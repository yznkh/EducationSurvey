using SocSurvey2.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocSurvey2.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.IO;

namespace SocSurvey2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SurveyController : ControllerBase
    {
        private readonly SurveyContext _context;

        public SurveyController(SurveyContext context)
        {
            _context = context;
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitSurvey([FromBody] SurveySubmissionDto surveyDto)
        {
            try
            {
                if (surveyDto == null)
                    return BadRequest("بيانات الاستبيان مطلوبة");

                if (string.IsNullOrWhiteSpace(surveyDto.Sector) ||
                    !new[] { "education", "health" }.Contains(surveyDto.Sector.ToLower()))
                    return BadRequest("القطاع غير صحيح");

                if (surveyDto.Answers == null || !surveyDto.Answers.Any())
                    return BadRequest("يجب تقديم إجابات للاستبيان");

                var validQuestionIds = await _context.Questions
                    .Where(q => q.Sector == surveyDto.Sector.ToLower())
                    .Select(q => q.Id)
                    .ToListAsync();

                foreach (var answer in surveyDto.Answers)
                {
                    if (!validQuestionIds.Contains(answer.QuestionId))
                        return BadRequest($"السؤال رقم {answer.QuestionId} غير صالح لهذا القطاع");
                }

                // استخلاص OrganizationName من الإجابات (افتراض: QuestionId = X لسؤال اسم المؤسسة)
                var orgNameAnswer = surveyDto.Answers.FirstOrDefault(a => a.QuestionId == 1); // استبدل X بمعرف السؤال الفعلي
                if (orgNameAnswer == null || string.IsNullOrWhiteSpace(orgNameAnswer.Value))
                    return BadRequest("اسم المؤسسة مطلوب");

                var submission = new SurveySubmission
                {
                    OrganizationName = orgNameAnswer.Value.Trim(),
                    SubmissionDate = DateTime.UtcNow,
                    Sector = surveyDto.Sector.ToLower(),
                    Answers = surveyDto.Answers.Select(a => new SurveyAnswer
                    {
                        QuestionId = a.QuestionId,
                        Value = a.Value?.Trim() ?? string.Empty
                    }).ToList()
                };

                _context.SurveySubmissions.Add(submission);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Message = "تم تقديم الاستبيان بنجاح",
                    SubmissionId = submission.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "حدث خطأ أثناء حفظ الاستبيان",
                    details = ex.Message
                });
            }
        }

        [HttpGet("stats")]
        [Authorize]
        public async Task<IActionResult> GetStats([FromQuery] string sector)
        {
            if (string.IsNullOrEmpty(sector) || !new[] { "education", "health" }.Contains(sector.ToLower()))
            {
                return BadRequest(new { message = "يرجى تحديد قطاع صحيح (education أو health)" });
            }

            sector = sector.ToLower();
            var submissions = await _context.SurveySubmissions
                .Include(s => s.Answers)
                .Where(s => s.Sector == sector)
                .ToListAsync();

            var totalSubmissions = submissions.Count;
            var totalOrganizations = submissions.Select(s => s.OrganizationName).Distinct().Count();

            // افتراض: السؤال ID=1 يسأل عما إذا كان لديهم SOC (Yes/No)
            var socAdoptionCount = submissions
                .SelectMany(s => s.Answers)
                .Count(a => a.QuestionId == 1 && a.Value.ToLower() == "yes");
            var socAdoptionRate = totalSubmissions > 0 ? (socAdoptionCount * 100.0 / totalSubmissions) : 0;

            // افتراض: السؤال ID=2 يسأل عن تغطية IT (نسبة)
            var itCoverageValues = submissions
                .SelectMany(s => s.Answers)
                .Where(a => a.QuestionId == 2 && double.TryParse(a.Value.Replace("%", ""), out _))
                .Select(a => double.Parse(a.Value.Replace("%", "")))
                .ToList();
            var avgItCoverage = itCoverageValues.Any() ? itCoverageValues.Average() : 0;

            // افتراض: السؤال ID=3 يسأل عن تغطية OT/IoT (نسبة)
            var otCoverageValues = submissions
                .SelectMany(s => s.Answers)
                .Where(a => a.QuestionId == 3 && double.TryParse(a.Value.Replace("%", ""), out _))
                .Select(a => double.Parse(a.Value.Replace("%", "")))
                .ToList();
            var avgOtCoverage = otCoverageValues.Any() ? otCoverageValues.Average() : 0;

            // افتراض: السؤال ID=4 يسأل عن حجم فريق SOC (عدد)
            var socTeamSizes = submissions
                .SelectMany(s => s.Answers)
                .Where(a => a.QuestionId == 4 && int.TryParse(a.Value, out _))
                .Select(a => int.Parse(a.Value))
                .ToList();
            var avgSocTeamSize = socTeamSizes.Any() ? socTeamSizes.Average() : 0;

            // افتراض: السؤال ID=5 يسأل عن تشغيل 24/7
            var soc247Count = submissions
                .SelectMany(s => s.Answers)
                .Count(a => a.QuestionId == 5 && a.Value.ToLower() == "yes");
            var soc247Rate = totalSubmissions > 0 ? (soc247Count * 100.0 / totalSubmissions) : 0;

            // افتراض: السؤال ID=6 يسأل عن دمج الاستخبارات التهديدية (Yes/No)
            var threatIntelCount = submissions
                .SelectMany(s => s.Answers)
                .Count(a => a.QuestionId == 6 && a.Value.ToLower() == "yes");
            var threatIntelRate = totalSubmissions > 0 ? (threatIntelCount * 100.0 / totalSubmissions) : 0;

            // توزيع الإجابات (للسؤال ID=1 كمثال)
            var answerDistribution = submissions
                .SelectMany(s => s.Answers)
                .Where(a => a.QuestionId == 1)
                .GroupBy(a => a.Value)
                .Select(g => new { Label = g.Key, Value = g.Count() })
                .ToList();

            // الاستبيانات حسب التاريخ
            var surveysByDate = submissions
                .GroupBy(s => s.SubmissionDate.Date)
                .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), Value = g.Count() })
                .OrderBy(g => g.Date)
                .ToList();

            return Ok(new
            {
                totalSubmissions,
                totalOrganizations,
                socAdoptionRate = Math.Round(socAdoptionRate, 1),
                avgItCoverage = Math.Round(avgItCoverage, 1),
                avgOtCoverage = Math.Round(avgOtCoverage, 1),
                avgSocTeamSize = Math.Round(avgSocTeamSize, 1),
                soc247Rate = Math.Round(soc247Rate, 1),
                threatIntelRate = Math.Round(threatIntelRate, 1),
                lastUpdated = submissions.Any() ? submissions.Max(s => s.SubmissionDate).ToString("g") : null,
                answerDistribution = new
                {
                    labels = answerDistribution.Select(x => x.Label).ToArray(),
                    values = answerDistribution.Select(x => x.Value).ToArray()
                },
                surveysByDate = new
                {
                    labels = surveysByDate.Select(s => s.Date).ToArray(),
                    values = surveysByDate.Select(s => s.Value).ToArray()
                }
            });
        }

        [HttpGet("questions")]
        public async Task<IActionResult> GetQuestions([FromQuery] string sector)
        {
            if (string.IsNullOrWhiteSpace(sector) || !new[] { "education", "health" }.Contains(sector.ToLower()))
                return BadRequest("يرجى تحديد قطاع صحيح");

            sector = sector.ToLower();

            var questions = await _context.Questions
                .Where(q => q.Sector == sector)
                .OrderBy(q => q.Id)
                .Select(q => new
                {
                    id = q.Id,
                    text = q.Text,
                    type = q.Type.ToLower(), // مثال: text, radio, dropdown
                    required = q.IsRequired,
                    options = _context.QuestionOptions
                                .Where(o => o.QuestionId == q.Id)
                                .OrderBy(o => o.Order)
                                .Select(o => o.OptionText)
                                .ToList()
                })
                .ToListAsync();

            return Ok(questions);
        }

        [HttpGet("submissions")]
        [Authorize]
        public async Task<IActionResult> GetSubmissions([FromQuery] string sector)
        {
            if (string.IsNullOrEmpty(sector) || !new[] { "education", "health" }.Contains(sector.ToLower()))
            {
                return BadRequest(new { message = "يرجى تحديد قطاع صحيح (education أو health)" });
            }

            sector = sector.ToLower();
            var submissions = await _context.SurveySubmissions
                .Where(s => s.Sector == sector)
                .Select(s => new
                {
                    s.Id,
                    s.OrganizationName,
                    s.SubmissionDate,
                    s.Sector
                })
                .ToListAsync();

            return Ok(submissions);
        }

        [HttpGet("export-full")]
        [Authorize]
        public async Task<IActionResult> ExportFullData([FromQuery] string sector)
        {
            if (string.IsNullOrEmpty(sector) || !new[] { "education", "health" }.Contains(sector.ToLower()))
            {
                return BadRequest("يرجى تحديد قطاع صحيح (education أو health)");
            }

            sector = sector.ToLower();

            try
            {
                // جلب جميع البيانات المطلوبة
                var submissions = await _context.SurveySubmissions
                    .Include(s => s.Answers)
                    .Where(s => s.Sector == sector)
                    .OrderBy(s => s.SubmissionDate)
                    .ToListAsync();

                // جلب جميع الأسئلة المتاحة
                var allQuestions = await _context.Questions
                    .Where(q => q.Sector == sector)
                    .OrderBy(q => q.Id)
                    .ToListAsync();

                // إنشاء DataTable
                var dt = new System.Data.DataTable();

                // إضافة الأعمدة الأساسية
                dt.Columns.Add("المؤسسة", typeof(string));
                dt.Columns.Add("تاريخ التقديم", typeof(string));
                dt.Columns.Add("القطاع", typeof(string));

                // إضافة أعمدة الأسئلة
                foreach (var question in allQuestions)
                {
                    dt.Columns.Add(question.Text, typeof(string));
                }

                // ملء البيانات
                foreach (var submission in submissions)
                {
                    var row = dt.NewRow();
                    row["المؤسسة"] = submission.OrganizationName;
                    row["تاريخ التقديم"] = submission.SubmissionDate.ToString("yyyy-MM-dd HH:mm");
                    row["القطاع"] = submission.Sector == "education" ? "التعليم" : "الصحة";

                    foreach (var question in allQuestions)
                    {
                        var answer = submission.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
                        row[question.Text] = answer?.Value ?? "N/A";
                    }

                    dt.Rows.Add(row);
                }

                // إنشاء ملف Excel باستخدام EPPlus
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("نتائج الاستبيان");

                    // تحميل البيانات من DataTable
                    worksheet.Cells["A1"].LoadFromDataTable(dt, true);

                    // تنسيق الرأس
                    using (var range = worksheet.Cells[1, 1, 1, dt.Columns.Count])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    // ضبط عرض الأعمدة تلقائياً
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    return File(stream,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"استبيان_الأمن_السيبراني_{sector}_{DateTime.Now:yyyyMMdd}.xlsx");
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "حدث خطأ أثناء التصدير" });
            }
        }

        // DTO for survey submission
        public class SurveySubmissionDto
        {
            public string Sector { get; set; }
            public List<AnswerDto> Answers { get; set; }
        }

        public class AnswerDto
        {
            public int QuestionId { get; set; }
            public string Value { get; set; }
        }
    }
}