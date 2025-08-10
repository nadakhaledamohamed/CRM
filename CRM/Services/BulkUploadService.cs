using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using CRM.Models;
using System.ComponentModel;


namespace CRM.Services
{
    public interface IBulkUploadService
    {
        Task<BulkUploadResult> ProcessExcelFileAsync(IFormFile file, int userId);
        Task<byte[]> GenerateTemplateAsync();
    }

    public class BulkUploadService : IBulkUploadService
    {
        private readonly CallCenterContext _context;
        private readonly Dictionary<string, int> _columnMapping;

        public BulkUploadService(CallCenterContext context)
        {
            _context = context;

            // Define column mappings (column name -> column index)
            _columnMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "FirstName", 1 },
                { "Email", 2 },
                { "Phone", 3 },
                { "WhatsApp", 4 },
                { "NationalId", 5 },
                { "UserType", 6 },
                { "HighSchool", 7 },
                { "Certificate", 8 },
                { "City", 9 },
                { "Grade", 10 },
                { "Nationality", 11 },
                { "HowDidYouKnowUs", 12 },
                { "MajorInterest1", 13 },
                { "MajorInterest2", 14 },
                { "RequestReason", 15 },
                { "RequestStatus", 16 },
                { "Comments", 17 }
            };
        }

        public async Task<BulkUploadResult> ProcessExcelFileAsync(IFormFile file, int userId)
        {
            var result = new BulkUploadResult();

            // Set license context for EPPlus
         //   ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
            {
                result.Errors.Add("No worksheet found in the Excel file.");
                return result;
            }

            try
            {
                // Load lookup data for validation and mapping
                var lookupData = await LoadLookupDataAsync();

                // Get current academic setting
                var academicSetting = await _context.AcademicSettings
                    .FirstOrDefaultAsync(a => a.IsActive == true);

                if (academicSetting == null)
                {
                    result.Errors.Add("No active academic setting found. Please configure academic settings first.");
                    return result;
                }

                // Process rows (assuming row 1 is header)
                var rowCount = worksheet.Dimension?.Rows ?? 0;

                for (int row = 2; row <= rowCount; row++)
                {
                    try
                    {
                        // Check if the row is empty
                        if (IsRowEmpty(worksheet, row))
                            continue;

                        var rowResult = await ProcessRowAsync(worksheet, row, userId, lookupData, academicSetting);

                        if (rowResult.Success)
                        {
                            result.SuccessCount++;
                        }
                        else
                        {
                            result.Errors.AddRange(rowResult.Errors.Select(e => $"Row {row}: {e}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Row {row}: Unexpected error - {ex.Message}");

                        // Log the inner exception for debugging
                        if (ex.InnerException != null)
                        {
                            result.Errors.Add($"Row {row}: Inner error - {ex.InnerException.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error loading lookup data or processing file: {ex.Message}");

                // Check for the specific duplicate key error
                if (ex.Message.Contains("An item with the same key has already been added"))
                {
                    result.Errors.Add("Database contains duplicate entries in lookup tables. Please contact administrator to clean up duplicate data.");
                }
            }

            return result;
        }

        private async Task<RowProcessResult> ProcessRowAsync(
            ExcelWorksheet worksheet,
            int row,
            int userId,
            LookupData lookupData,
            AcademicSetting academicSetting)
        {
            var rowResult = new RowProcessResult();
            var validationErrors = new List<string>();

            try
            {
                // Extract data from row with safe null handling
                var rowData = ExtractRowData(worksheet, row);

                // Validate required fields
                if (string.IsNullOrWhiteSpace(rowData.FirstName))
                {
                    validationErrors.Add("First Name is required");
                }

                // Validate email format (only if provided)
                if (!string.IsNullOrWhiteSpace(rowData.Email))
                {
                    if (!IsValidEmail(rowData.Email))
                    {
                        validationErrors.Add($"Invalid email format: {rowData.Email}");
                    }
                    else
                    {
                        // Check for duplicate email
                        var emailExists = await _context.People
                            .AnyAsync(p => p.Email.ToLower() == rowData.Email.ToLower());

                        if (emailExists)
                        {
                            validationErrors.Add($"Email already exists: {rowData.Email}");
                        }
                    }
                }

                // Validate phone (make it optional based on your requirements)
                if (!string.IsNullOrWhiteSpace(rowData.Phone))
                {
                    // Clean phone number
                    rowData.Phone = CleanPhoneNumber(rowData.Phone);

                    // Check for duplicate phone
                    var phoneExists = await _context.People
                        .AnyAsync(p => p.Phone == rowData.Phone);

                    if (phoneExists)
                    {
                        validationErrors.Add($"Phone number already exists: {rowData.Phone}");
                    }
                }

                // Validate National ID (only if provided)
                if (!string.IsNullOrWhiteSpace(rowData.NationalId))
                {
                    var nationalIdExists = await _context.People
                        .AnyAsync(p => p.NationalId == rowData.NationalId);

                    if (nationalIdExists)
                    {
                        validationErrors.Add($"National ID already exists: {rowData.NationalId}");
                    }
                }

                // If there are validation errors, return them
                if (validationErrors.Any())
                {
                    rowResult.Errors.AddRange(validationErrors);
                    return rowResult;
                }

                // Map lookup values
                var mappedData = await MapLookupValuesAsync(rowData, lookupData);

                // Create Person entity
                var person = new Person
                {
                    FirstName = rowData.FirstName,
                    Email = string.IsNullOrWhiteSpace(rowData.Email) ? null : rowData.Email,
                    Phone = string.IsNullOrWhiteSpace(rowData.Phone) ? null : rowData.Phone,
                    whatsApp = string.IsNullOrWhiteSpace(rowData.WhatsApp) ? null : rowData.WhatsApp,
                    NationalId = string.IsNullOrWhiteSpace(rowData.NationalId) ? null : rowData.NationalId,
                    UserType = mappedData.UserType ?? 1, // Default to Lead if not specified
                    HighSchoolId = mappedData.HighSchoolId,
                    CertificateId = mappedData.CertificateId,
                    CityID = mappedData.CityId,
                    GradeID = mappedData.GradeId,
                    NationalityID = mappedData.NationalityId,
                    HowDidYouKnowUsId = mappedData.HowDidYouKnowUsId,
                    HowDidYouKnowUs_Other = mappedData.HowDidYouKnowUsOther,
                    CreatedAt = DateTime.Now,
                    CreatedByCode = userId
                };

                _context.People.Add(person);
                await _context.SaveChangesAsync();

                // Add Major Interests if provided
                if (mappedData.MajorId1.HasValue)
                {
                    _context.MajorPersons.Add(new MajorPerson
                    {
                        PersonID = person.PersonId,
                        MajorID = mappedData.MajorId1.Value,
                        Academic_Setting_ID = academicSetting.AcademicSettingId,
                        Priority = 1
                    });
                }

                if (mappedData.MajorId2.HasValue && mappedData.MajorId2 != mappedData.MajorId1)
                {
                    _context.MajorPersons.Add(new MajorPerson
                    {
                        PersonID = person.PersonId,
                        MajorID = mappedData.MajorId2.Value,
                        Academic_Setting_ID = academicSetting.AcademicSettingId,
                        Priority = 2
                    });
                }

                // Create Request if reason is provided
                if (mappedData.ReasonId.HasValue)
                {
                    var request = new Request
                    {
                        PersonId = person.PersonId,
                        ReasonID = mappedData.ReasonId.Value,
                        StatusId = mappedData.StatusId ?? 1, // Default to status 1 if not provided
                        Comments = rowData.Comments,
                        FollowUpCount = 0,
                        CreatedAt = DateTime.Now,
                        CreatedByCode = userId
                    };

                    _context.Requests.Add(request);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Log why request wasn't created
                    if (!string.IsNullOrWhiteSpace(rowData.RequestReasonText))
                    {
                        rowResult.Errors.Add($"Warning: Could not create request. Reason '{rowData.RequestReasonText}' was not found or created.");
                    }
                }

                rowResult.Success = true;
            }
            catch (Exception ex)
            {
                rowResult.Errors.Add($"Error processing row: {ex.Message}");
            }

            return rowResult;
        }

        private BulkUploadRowData ExtractRowData(ExcelWorksheet worksheet, int row)
        {
            return new BulkUploadRowData
            {
                FirstName = GetCellValue(worksheet, row, _columnMapping["FirstName"]),
                Email = GetCellValue(worksheet, row, _columnMapping["Email"]),
                Phone = GetCellValue(worksheet, row, _columnMapping["Phone"]),
                WhatsApp = GetCellValue(worksheet, row, _columnMapping["WhatsApp"]),
                NationalId = GetCellValue(worksheet, row, _columnMapping["NationalId"]),
                UserTypeText = GetCellValue(worksheet, row, _columnMapping["UserType"]),
                HighSchoolText = GetCellValue(worksheet, row, _columnMapping["HighSchool"]),
                CertificateText = GetCellValue(worksheet, row, _columnMapping["Certificate"]),
                CityText = GetCellValue(worksheet, row, _columnMapping["City"]),
                GradeText = GetCellValue(worksheet, row, _columnMapping["Grade"]),
                NationalityText = GetCellValue(worksheet, row, _columnMapping["Nationality"]),
                HowDidYouKnowUsText = GetCellValue(worksheet, row, _columnMapping["HowDidYouKnowUs"]),
                MajorInterest1Text = GetCellValue(worksheet, row, _columnMapping["MajorInterest1"]),
                MajorInterest2Text = GetCellValue(worksheet, row, _columnMapping["MajorInterest2"]),
                RequestReasonText = GetCellValue(worksheet, row, _columnMapping["RequestReason"]),
                RequestStatusText = GetCellValue(worksheet, row, _columnMapping["RequestStatus"]),
                Comments = GetCellValue(worksheet, row, _columnMapping["Comments"])
            };
        }

        private string GetCellValue(ExcelWorksheet worksheet, int row, int col)
        {
            var value = worksheet.Cells[row, col].Value?.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private bool IsRowEmpty(ExcelWorksheet worksheet, int row)
        {
            // Check if all important columns are empty
            var firstName = GetCellValue(worksheet, row, _columnMapping["FirstName"]);
            var email = GetCellValue(worksheet, row, _columnMapping["Email"]);
            var phone = GetCellValue(worksheet, row, _columnMapping["Phone"]);

            return string.IsNullOrWhiteSpace(firstName) &&
                   string.IsNullOrWhiteSpace(email) &&
                   string.IsNullOrWhiteSpace(phone);
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private string CleanPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return phone;

            // Remove common formatting characters
            return phone.Replace(" ", "")
                       .Replace("-", "")
                       .Replace("(", "")
                       .Replace(")", "")
                       .Replace("+", "");
        }

        private async Task<LookupData> LoadLookupDataAsync()
        {
            // Load data from database first, then convert to dictionaries handling duplicates
            var highSchools = await _context.LookUpHighSchools.ToListAsync();
            var certificates = await _context.LookUpHighSchoolCerts.ToListAsync();
            var cities = await _context.Lookup_City.ToListAsync();
            var grades = await _context.LookUp_Grade.ToListAsync();
            var nationalities = await _context.Lookup_Nationality.ToListAsync();
            var howDidYouKnowUs = await _context.LookUpHowDidYouKnowUs.ToListAsync();
            var majors = await _context.LookupMajors.ToListAsync();
            var reasons = await _context.Lookup_ReasonDescription.ToListAsync();
            var statuses = await _context.LookUpStatusTypes.ToListAsync();

            return new LookupData
            {
                HighSchools = highSchools
                    .GroupBy(h => h.HighSchoolName?.ToLower() ?? "")
                    .ToDictionary(g => g.Key, g => g.First().HighSchoolId),

                Certificates = certificates
                    .GroupBy(c => c.CertificateName?.ToLower() ?? "")
                    .ToDictionary(g => g.Key, g => g.First().CertificateId),

                Cities = cities
                    .GroupBy(c => c.CityName?.ToLower() ?? "")
                    .ToDictionary(g => g.Key, g => g.First().CityID),

                Grades = grades
                    .GroupBy(g => g.GradeName?.ToLower() ?? "")
                    .ToDictionary(g => g.Key, g => g.First().GradeID),

                Nationalities = nationalities
                    .GroupBy(n => n.NationalityName?.ToLower() ?? "")
                    .ToDictionary(g => g.Key, g => g.First().NationalityID),

                HowDidYouKnowUs = howDidYouKnowUs
                    .GroupBy(h => h.HowDidYouKnowUs?.ToLower() ?? "")
                    .ToDictionary(g => g.Key, g => g.First().HowDidYouKnowUsId),

                Majors = majors
                    .GroupBy(m => m.MajorInterest?.ToLower() ?? "")
                    .ToDictionary(g => g.Key, g => g.First().MajorId),

                Reasons = reasons
                    .GroupBy(r => r.Reason_Description?.ToLower() ?? "")
                    .ToDictionary(g => g.Key, g => g.First().ReasonID),

                Statuses = statuses
                    .GroupBy(s => s.StatusName?.ToLower() ?? "")
                    .ToDictionary(g => g.Key, g => g.First().StatusId)
            };
        }

        private async Task<MappedLookupData> MapLookupValuesAsync(BulkUploadRowData rowData, LookupData lookupData)
        {
            var mapped = new MappedLookupData();

            // Map UserType
            if (!string.IsNullOrWhiteSpace(rowData.UserTypeText))
            {
                var userTypeText = rowData.UserTypeText.ToLower().Trim();
                if (userTypeText == "lead" || userTypeText == "1")
                    mapped.UserType = 1;
                else if (userTypeText == "guardian" || userTypeText == "2")
                    mapped.UserType = 2;
            }

            // Map HighSchool (create if doesn't exist)
            if (!string.IsNullOrWhiteSpace(rowData.HighSchoolText))
            {
                var key = rowData.HighSchoolText.Trim().ToLower();
                if (!string.IsNullOrEmpty(key) && lookupData.HighSchools.ContainsKey(key))
                {
                    mapped.HighSchoolId = lookupData.HighSchools[key];
                }
                else if (!string.IsNullOrEmpty(key))
                {
                    // Check if it already exists in the database (case-insensitive)
                    var existingHighSchool = await _context.LookUpHighSchools
                        .FirstOrDefaultAsync(h => h.HighSchoolName.ToLower() == key);

                    if (existingHighSchool != null)
                    {
                        mapped.HighSchoolId = existingHighSchool.HighSchoolId;
                        // Add to cache for next rows
                        lookupData.HighSchools[key] = existingHighSchool.HighSchoolId;
                    }
                    else
                    {
                        // Create new high school
                        var newHighSchool = new LookUpHighSchool { HighSchoolName = rowData.HighSchoolText.Trim() };
                        _context.LookUpHighSchools.Add(newHighSchool);
                        await _context.SaveChangesAsync();
                        mapped.HighSchoolId = newHighSchool.HighSchoolId;
                        // Add to cache
                        lookupData.HighSchools[key] = newHighSchool.HighSchoolId;
                    }
                }
            }

            // Map other lookup fields with safe checking
            if (!string.IsNullOrWhiteSpace(rowData.CertificateText))
            {
                var key = rowData.CertificateText.Trim().ToLower();
                if (!string.IsNullOrEmpty(key) && lookupData.Certificates.ContainsKey(key))
                    mapped.CertificateId = lookupData.Certificates[key];
            }

            if (!string.IsNullOrWhiteSpace(rowData.CityText))
            {
                var key = rowData.CityText.Trim().ToLower();
                if (!string.IsNullOrEmpty(key) && lookupData.Cities.ContainsKey(key))
                    mapped.CityId = lookupData.Cities[key];
            }

            if (!string.IsNullOrWhiteSpace(rowData.GradeText))
            {
                var key = rowData.GradeText.Trim().ToLower();
                if (!string.IsNullOrEmpty(key) && lookupData.Grades.ContainsKey(key))
                    mapped.GradeId = lookupData.Grades[key];
            }

            if (!string.IsNullOrWhiteSpace(rowData.NationalityText))
            {
                var key = rowData.NationalityText.Trim().ToLower();
                if (!string.IsNullOrEmpty(key) && lookupData.Nationalities.ContainsKey(key))
                    mapped.NationalityId = lookupData.Nationalities[key];
            }

            // Map HowDidYouKnowUs (handle "Other" case)
            if (!string.IsNullOrWhiteSpace(rowData.HowDidYouKnowUsText))
            {
                var key = rowData.HowDidYouKnowUsText.Trim().ToLower();
                if (!string.IsNullOrEmpty(key))
                {
                    if (lookupData.HowDidYouKnowUs.ContainsKey(key))
                    {
                        mapped.HowDidYouKnowUsId = lookupData.HowDidYouKnowUs[key];
                    }
                    else
                    {
                        // Check if "Other" exists in the lookup
                        var otherKey = "other";
                        if (lookupData.HowDidYouKnowUs.ContainsKey(otherKey))
                        {
                            mapped.HowDidYouKnowUsId = lookupData.HowDidYouKnowUs[otherKey];
                            mapped.HowDidYouKnowUsOther = rowData.HowDidYouKnowUsText.Trim();
                        }
                        else
                        {
                            // Default to ID 8 if "Other" is not found
                            mapped.HowDidYouKnowUsId = 8;
                            mapped.HowDidYouKnowUsOther = rowData.HowDidYouKnowUsText.Trim();
                        }
                    }
                }
            }

            // Map Major Interests
            if (!string.IsNullOrWhiteSpace(rowData.MajorInterest1Text))
            {
                var key = rowData.MajorInterest1Text.Trim().ToLower();
                if (!string.IsNullOrEmpty(key) && lookupData.Majors.ContainsKey(key))
                    mapped.MajorId1 = lookupData.Majors[key];
            }

            if (!string.IsNullOrWhiteSpace(rowData.MajorInterest2Text))
            {
                var key = rowData.MajorInterest2Text.Trim().ToLower();
                if (!string.IsNullOrEmpty(key) && lookupData.Majors.ContainsKey(key))
                    mapped.MajorId2 = lookupData.Majors[key];
            }

            // Map Request Reason (create if doesn't exist)
            if (!string.IsNullOrWhiteSpace(rowData.RequestReasonText))
            {
                var key = rowData.RequestReasonText.Trim().ToLower();
                if (!string.IsNullOrEmpty(key))
                {
                    if (lookupData.Reasons.ContainsKey(key))
                    {
                        mapped.ReasonId = lookupData.Reasons[key];
                    }
                    else
                    {
                        // Check if it already exists in the database
                        var existingReason = await _context.Lookup_ReasonDescription
                            .FirstOrDefaultAsync(r => r.Reason_Description.ToLower() == key);

                        if (existingReason != null)
                        {
                            mapped.ReasonId = existingReason.ReasonID;
                            // Add to cache
                            lookupData.Reasons[key] = existingReason.ReasonID;
                        }
                        else
                        {
                            // Create new reason
                            var newReason = new Lookup_ReasonDescription { Reason_Description = rowData.RequestReasonText.Trim() };
                            _context.Lookup_ReasonDescription.Add(newReason);
                            await _context.SaveChangesAsync();
                            mapped.ReasonId = newReason.ReasonID;
                            // Add to cache
                            lookupData.Reasons[key] = newReason.ReasonID;
                        }
                    }
                }
            }

            // Map Status
            if (!string.IsNullOrWhiteSpace(rowData.RequestStatusText))
            {
                var key = rowData.RequestStatusText.Trim().ToLower();
                if (!string.IsNullOrEmpty(key) && lookupData.Statuses.ContainsKey(key))
                    mapped.StatusId = lookupData.Statuses[key];
            }

            return mapped;
        }

        public async Task<byte[]> GenerateTemplateAsync()
        {
            //ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Person Template");

            // Add headers
            var headers = new[]
            {
                "FirstName*", "Email", "Phone", "WhatsApp", "NationalId",
                "UserType", "HighSchool", "Certificate", "City", "Grade",
                "Nationality", "HowDidYouKnowUs", "MajorInterest1", "MajorInterest2",
                "RequestReason", "RequestStatus", "Comments"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                worksheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            // Add sample data
            worksheet.Cells[2, 1].Value = "John Doe";
            worksheet.Cells[2, 2].Value = "john.doe@example.com";
            worksheet.Cells[2, 3].Value = "01234567890";
            worksheet.Cells[2, 4].Value = "01234567890";
            worksheet.Cells[2, 5].Value = "12345678901234";
            worksheet.Cells[2, 6].Value = "Lead";
            worksheet.Cells[2, 7].Value = "Cairo High School";
            worksheet.Cells[2, 8].Value = "General Certificate";
            worksheet.Cells[2, 9].Value = "Cairo";
            worksheet.Cells[2, 10].Value = "Grade 12";
            worksheet.Cells[2, 11].Value = "Egyptian";
            worksheet.Cells[2, 12].Value = "Social Media";
            worksheet.Cells[2, 13].Value = "Engineering";
            worksheet.Cells[2, 14].Value = "Computer Science";
            worksheet.Cells[2, 15].Value = "Interested in program";
            worksheet.Cells[2, 16].Value = "Interested";
            worksheet.Cells[2, 17].Value = "Sample comment";

            // Add instructions sheet
            var instructionSheet = package.Workbook.Worksheets.Add("Instructions");
            instructionSheet.Cells[1, 1].Value = "BULK UPLOAD INSTRUCTIONS";
            instructionSheet.Cells[1, 1].Style.Font.Bold = true;
            instructionSheet.Cells[1, 1].Style.Font.Size = 14;

            var instructions = new[]
            {
                "",
                "REQUIRED FIELDS:",
                "- FirstName: Person's first name (required)",
                "",
                "OPTIONAL FIELDS:",
                "- Email: Valid email address",
                "- Phone: Phone number",
                "- WhatsApp: WhatsApp number",
                "- NationalId: National ID or Passport number",
                "- UserType: 'Lead' or 'Guardian'",
                "- HighSchool: Name of high school (will be created if doesn't exist)",
                "- Certificate: Certificate name",
                "- City: City name",
                "- Grade: Grade level",
                "- Nationality: Nationality",
                "- HowDidYouKnowUs: How they heard about you",
                "- MajorInterest1: Primary major interest",
                "- MajorInterest2: Secondary major interest",
                "- RequestReason: Reason for request (will be created if doesn't exist)",
                "- RequestStatus: Status name",
                "- Comments: Any additional comments",
                "",
                "NOTES:",
                "- The system will check for duplicate emails, phones, and national IDs",
                "- New lookup values will be created automatically for HighSchool and RequestReason",
                "- Leave cells empty if data is not available",
                "- Do not modify the header row"
            };

            for (int i = 0; i < instructions.Length; i++)
            {
                instructionSheet.Cells[i + 2, 1].Value = instructions[i];
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            instructionSheet.Cells[instructionSheet.Dimension.Address].AutoFitColumns();

            return package.GetAsByteArray();
        }
    }

    // Supporting classes
    public class BulkUploadResult
    {
        public int SuccessCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class RowProcessResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class BulkUploadRowData
    {
        public string FirstName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string WhatsApp { get; set; }
        public string NationalId { get; set; }
        public string UserTypeText { get; set; }
        public string HighSchoolText { get; set; }
        public string CertificateText { get; set; }
        public string CityText { get; set; }
        public string GradeText { get; set; }
        public string NationalityText { get; set; }
        public string HowDidYouKnowUsText { get; set; }
        public string MajorInterest1Text { get; set; }
        public string MajorInterest2Text { get; set; }
        public string RequestReasonText { get; set; }
        public string RequestStatusText { get; set; }
        public string Comments { get; set; }
    }

    public class LookupData
    {
        public Dictionary<string, int> HighSchools { get; set; }
        public Dictionary<string, int> Certificates { get; set; }
        public Dictionary<string, int> Cities { get; set; }
        public Dictionary<string, int> Grades { get; set; }
        public Dictionary<string, int> Nationalities { get; set; }
        public Dictionary<string, int> HowDidYouKnowUs { get; set; }
        public Dictionary<string, int> Majors { get; set; }
        public Dictionary<string, int> Reasons { get; set; }
        public Dictionary<string, int> Statuses { get; set; }
    }

    public class MappedLookupData
    {
        public int? UserType { get; set; }
        public int? HighSchoolId { get; set; }
        public int? CertificateId { get; set; }
        public int? CityId { get; set; }
        public int? GradeId { get; set; }
        public int? NationalityId { get; set; }
        public int? HowDidYouKnowUsId { get; set; }
        public string HowDidYouKnowUsOther { get; set; }
        public int? MajorId1 { get; set; }
        public int? MajorId2 { get; set; }
        public int? ReasonId { get; set; }
        public int? StatusId { get; set; }
    }
}