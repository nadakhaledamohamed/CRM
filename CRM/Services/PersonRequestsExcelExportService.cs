using OfficeOpenXml;
using OfficeOpenXml.Style;
using CRM.Models;
using System.Drawing;
using Microsoft.EntityFrameworkCore;

//Install-Package EPPlus -Version 4.5.3.3 is for free 

namespace CRM.Services
{
    public class PersonRequestsExcelExportService
    {
        private readonly CallCenterContext _context;
        private readonly Dictionary<int, string> _userTypeOptions;

        public PersonRequestsExcelExportService(CallCenterContext context, Dictionary<int, string> userTypeOptions = null)
        {
            _context = context;
            _userTypeOptions = userTypeOptions ?? new Dictionary<int, string>();
        }

        public async Task<byte[]> ExportToExcelAsync(List<int> personIds, string fileName = "PersonRequests")
        {
            // Fetch complete data with all relationships
            var completePersonData = await GetCompletePersonDataAsync(personIds);

            using (var package = new ExcelPackage())
            {
                // Create Summary Sheet
                await CreateSummarySheetAsync(package, completePersonData);

                // Create Main Data Sheet
                CreateMainDataSheet(package, completePersonData);

                // Create Details Sheet
                CreateDetailsSheet(package, completePersonData);

                // Create Follow-up Tracking Sheet
                CreateFollowUpSheet(package, completePersonData);

                // Create Major Interests Sheet
                CreateMajorInterestsSheet(package, completePersonData);

                return package.GetAsByteArray();
            }
        }

        private async Task<List<CompletePersonRequestData>> GetCompletePersonDataAsync(List<int> personIds)
        {
            var completeData = new List<CompletePersonRequestData>();

            // Get all users for lookups
            var userDictionary = await _context.Users
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Get all status types
            var statusTypes = await _context.LookUpStatusTypes
                .ToDictionaryAsync(s => s.StatusId, s => s.StatusName);

            foreach (var personId in personIds)
            {
                // Get person with all relationships
                var person = await _context.People
                    .Include(p => p.HighSchool)
                    .Include(p => p.Certificate)
                    .Include(p => p.HowDidYouKnowUs)
                    .Include(p => p.City)
                    .Include(p => p.Grade)
                    .Include(p => p.Nationality)
                    .FirstOrDefaultAsync(p => p.PersonId == personId);

                if (person == null) continue;

                // Get person's major interests
                var majorInterestIds = await GetPersonMajorInterestsAsync(person.PersonId);
                var majorInterests = new List<string>();
                if (majorInterestIds.Any())
                {
                    majorInterests = await _context.LookupMajors
                        .Where(m => majorInterestIds.Contains(m.MajorId))
                        .Select(m => m.MajorInterest)
                        .ToListAsync();
                }

                // Handle HowDidYouKnowUs display
                string howDidYouKnowUsDisplay;
                if (person.HowDidYouKnowUsId == 8) // "Other" option
                {
                    howDidYouKnowUsDisplay = !string.IsNullOrEmpty(person.HowDidYouKnowUs_Other)
                        ? person.HowDidYouKnowUs_Other
                        : "Other";
                }
                else
                {
                    howDidYouKnowUsDisplay = person.HowDidYouKnowUs?.HowDidYouKnowUs ?? "Not specified";
                }

                // Get requests with all details
                var requests = await _context.Requests
                    .Where(r => r.PersonId == personId)
                    .Include(r => r.Reason)
                    .ToListAsync();

                // Create complete data objects
                foreach (var request in requests)
                {
                    var completePersonRequest = new CompletePersonRequestData
                    {
                        // Person data
                        PersonId = person.PersonId,
                        FirstName = person.FirstName,
                     //   LastName = person.LastName,
                        Email = person.Email,
                        Phone = person.Phone,
                        NationalId = person.NationalId,
                        UserType = person.UserType,
                        UserTypeName = GetUserTypeName(person.UserType),
                        PersonCreatedAt = person.CreatedAt,
                        PersonCreatedByName = userDictionary.TryGetValue(person.CreatedByCode, out var personCreated) ? personCreated : "Unknown",
                        PersonUpdatedAt = person.UpdatedAt,
                        PersonUpdatedByName = person.UpdatedByCode.HasValue && userDictionary.TryGetValue(person.UpdatedByCode.Value, out var personUpdated) ? personUpdated : "N/A",

                        // Lookup data
                        HighSchoolName = person.HighSchool?.HighSchoolName ?? "N/A",
                        CertificateName = person.Certificate?.CertificateName ?? "N/A",
                        HowDidYouKnowUsDisplay = howDidYouKnowUsDisplay,
                        CityName = person.City?.CityName ?? "N/A",
                        GradeName = person.Grade?.GradeName ?? "N/A",
                        NationalityName = person.Nationality?.NationalityName ?? "N/A",
                        MajorInterests = majorInterests,
                        PrimaryMajor = majorInterests.FirstOrDefault() ?? "Not specified",
                        AllMajorsString = majorInterests.Any() ? string.Join(", ", majorInterests) : "Not specified",

                        // Request data
                        RequestId = request.RequestId,
                        ReasonId = (int)request.ReasonID,
                        ReasonDescription = request.Reason?.Reason_Description ?? "N/A",
                        Comments = request.Comments ?? "",
                        FollowUpCount = request.FollowUpCount ?? 0,
                        LastFollowUpDate = request.LastFollowUpDate,
                        RequestCreatedAt = request.CreatedAt,
                        RequestUpdatedAt = (DateTime)request.UpdatedAt,
                        RequestCreatedByName = userDictionary.TryGetValue(request.CreatedByCode, out var requestCreated) ? requestCreated : "Unknown",
                        RequestUpdatedByName = request.UpdatedByCode.HasValue && userDictionary.TryGetValue(request.UpdatedByCode.Value, out var requestUpdated) ? requestUpdated : "N/A",
                        StatusId = (int)request.StatusId,
                        StatusName = statusTypes.TryGetValue((int)request.StatusId, out var statusName) ? statusName : "N/A",

                        // Calculated fields
                        IsFollowUpOverdue = IsFollowUpOverdue(request),
                        RequiresFollowUp = RequiresFollowUp(request),
                        IsRequestClosed = IsRequestClosed(request)
                    };

                    completeData.Add(completePersonRequest);
                }

                // If person has no requests, still add them to the export
                if (!requests.Any())
                {
                    var completePersonRequest = new CompletePersonRequestData
                    {
                        // Person data
                        PersonId = person.PersonId,
                        FirstName = person.FirstName,
                      //  LastName = person.LastName,
                        Email = person.Email,
                        Phone = person.Phone,
                        NationalId = person.NationalId,
                        UserType = person.UserType,
                        UserTypeName = GetUserTypeName(person.UserType),
                        PersonCreatedAt = person.CreatedAt,
                        PersonCreatedByName = userDictionary.TryGetValue(person.CreatedByCode, out var personCreated) ? personCreated : "Unknown",
                        PersonUpdatedAt = person.UpdatedAt,
                        PersonUpdatedByName = person.UpdatedByCode.HasValue && userDictionary.TryGetValue(person.UpdatedByCode.Value, out var personUpdated) ? personUpdated : "N/A",

                        // Lookup data
                        HighSchoolName = person.HighSchool?.HighSchoolName ?? "N/A",
                        CertificateName = person.Certificate?.CertificateName ?? "N/A",
                        HowDidYouKnowUsDisplay = howDidYouKnowUsDisplay,
                        CityName = person.City?.CityName ?? "N/A",
                        GradeName = person.Grade?.GradeName ?? "N/A",
                        NationalityName = person.Nationality?.NationalityName ?? "N/A",
                        MajorInterests = majorInterests,
                        PrimaryMajor = majorInterests.FirstOrDefault() ?? "Not specified",
                        AllMajorsString = majorInterests.Any() ? string.Join(", ", majorInterests) : "Not specified",

                        // Request data - empty for persons without requests
                        RequestId = 0,
                        ReasonId = 0,
                        ReasonDescription = "No requests",
                        Comments = "",
                        FollowUpCount = 0,
                        LastFollowUpDate = null,
                        RequestCreatedAt = DateTime.MinValue,
                        RequestUpdatedAt = DateTime.MinValue,
                        RequestCreatedByName = "N/A",
                        RequestUpdatedByName = "N/A",
                        StatusId = 0,
                        StatusName = "N/A",

                        // Calculated fields
                        IsFollowUpOverdue = false,
                        RequiresFollowUp = false,
                        IsRequestClosed = false
                    };

                    completeData.Add(completePersonRequest);
                }
            }

            return completeData;
        }

        private async Task<List<int>> GetPersonMajorInterestsAsync(int personId)
        {
            
            // This should match your existing GetPersonMajorInterestsAsync method
            return await _context.MajorPersons
        .Where(pmi => pmi.PersonID == personId)
        .Where(pmi => pmi.MajorID.HasValue)  // Only include non-null MajorIDs
        .Select(pmi => pmi.MajorID.Value)    // Convert int? to int
        .ToListAsync();
        }

        private bool IsFollowUpOverdue(Request request)
        {
            // Implement your logic to determine if follow-up is overdue
            if (request.LastFollowUpDate.HasValue)
            {
                return (DateTime.Now - request.LastFollowUpDate.Value).Days > 30; // Example: 30 days
            }
            return (DateTime.Now - request.CreatedAt).Days > 30;
        }

        private bool RequiresFollowUp(Request request)
        {
            // Implement your logic to determine if follow-up is required
            // This might depend on status or other business rules
            return request.StatusId != 3; // Example: assuming status 3 is "Closed"
        }

        private bool IsRequestClosed(Request request)
        {
            // Implement your logic to determine if request is closed
            return request.StatusId == 3; // Example: assuming status 3 is "Closed"
        }

        private async Task CreateSummarySheetAsync(ExcelPackage package, List<CompletePersonRequestData> completeData)
        {
            var worksheet = package.Workbook.Worksheets.Add("Summary");

            // Title
            worksheet.Cells[1, 1].Value = "Person Requests Summary Report";
            worksheet.Cells[1, 1].Style.Font.Size = 16;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.DarkBlue);

            // Generation info
            worksheet.Cells[2, 1].Value = $"Generated on: {DateTime.Now:MMMM dd, yyyy 'at' HH:mm}";
            worksheet.Cells[3, 1].Value = $"Total Records: {completeData.Count}";
            worksheet.Cells[3, 1].Style.Font.Bold = true;

            // Status Summary
            var statusGroups = completeData
                .GroupBy(p => p.StatusName ?? "N/A")
                .ToDictionary(g => g.Key, g => g.Count());

            worksheet.Cells[5, 1].Value = "Summary by Status";
            worksheet.Cells[5, 1].Style.Font.Bold = true;
            worksheet.Cells[5, 1].Style.Font.Color.SetColor(Color.DarkGreen);

            int row = 6;
            worksheet.Cells[row, 1].Value = "Status";
            worksheet.Cells[row, 2].Value = "Count";
            worksheet.Cells[row, 1, row, 2].Style.Font.Bold = true;
            worksheet.Cells[row, 1, row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, 1, row, 2].Style.Fill.BackgroundColor.SetColor(Color.LightGray);

            foreach (var status in statusGroups)
            {
                row++;
                worksheet.Cells[row, 1].Value = status.Key;
                worksheet.Cells[row, 2].Value = status.Value;
            }

            // User Type Summary
            var userTypeGroups = completeData
                .GroupBy(p => p.UserTypeName)
                .ToDictionary(g => g.Key, g => g.Count());

            row += 2;
            worksheet.Cells[row, 1].Value = "Summary by User Type";
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.Font.Color.SetColor(Color.DarkGreen);

            row++;
            worksheet.Cells[row, 1].Value = "User Type";
            worksheet.Cells[row, 2].Value = "Count";
            worksheet.Cells[row, 1, row, 2].Style.Font.Bold = true;
            worksheet.Cells[row, 1, row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, 1, row, 2].Style.Fill.BackgroundColor.SetColor(Color.LightGray);

            foreach (var userType in userTypeGroups)
            {
                row++;
                worksheet.Cells[row, 1].Value = userType.Key;
                worksheet.Cells[row, 2].Value = userType.Value;
            }

            // City Summary
            var cityGroups = completeData
                .GroupBy(p => p.CityName)
                .ToDictionary(g => g.Key, g => g.Count());

            row += 2;
            worksheet.Cells[row, 1].Value = "Summary by City";
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.Font.Color.SetColor(Color.DarkGreen);

            row++;
            worksheet.Cells[row, 1].Value = "City";
            worksheet.Cells[row, 2].Value = "Count";
            worksheet.Cells[row, 1, row, 2].Style.Font.Bold = true;
            worksheet.Cells[row, 1, row, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, 1, row, 2].Style.Fill.BackgroundColor.SetColor(Color.LightGray);

            foreach (var city in cityGroups)
            {
                row++;
                worksheet.Cells[row, 1].Value = city.Key;
                worksheet.Cells[row, 2].Value = city.Value;
            }

            // Follow-up Statistics
            var overdueCount = completeData.Count(p => p.IsFollowUpOverdue);
            var pendingCount = completeData.Count(p => p.RequiresFollowUp && !p.IsFollowUpOverdue);
            var totalFollowUpsNeeded = completeData.Count(p => p.RequiresFollowUp);

            row += 2;
            worksheet.Cells[row, 1].Value = "Follow-up Statistics";
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.Font.Color.SetColor(Color.DarkRed);

            row++;
            worksheet.Cells[row, 1].Value = "Overdue Follow-ups";
            worksheet.Cells[row, 2].Value = overdueCount;
            worksheet.Cells[row + 1, 1].Value = "Pending Follow-ups";
            worksheet.Cells[row + 1, 2].Value = pendingCount;
            worksheet.Cells[row + 2, 1].Value = "Total Follow-ups Needed";
            worksheet.Cells[row + 2, 2].Value = totalFollowUpsNeeded;

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        private void CreateMainDataSheet(ExcelPackage package, List<CompletePersonRequestData> completeData)
        {
            var worksheet = package.Workbook.Worksheets.Add("Main Data");

            // Headers
            var headers = new string[]
            {
                "Person ID", "First Name", "Last Name", "Email", "Phone", "National ID",
                "User Type", "City", "Grade", "Nationality", "High School", "Certificate",
                "How Did You Know Us", "Primary Major", "All Majors",
                "Request ID", "Status", "Reason", "Follow-up Count", "Last Follow-up Date",
                "Is Follow-up Overdue", "Request Closed", "Person Created Date", "Person Created By",
                "Request Created Date", "Request Created By"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            // Style headers
            var headerRange = worksheet.Cells[1, 1, 1, headers.Length];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(Color.DarkBlue);
            headerRange.Style.Font.Color.SetColor(Color.White);

            // Data rows
            for (int i = 0; i < completeData.Count; i++)
            {
                var data = completeData[i];
                int row = i + 2;

                worksheet.Cells[row, 1].Value = data.PersonId;
                worksheet.Cells[row, 2].Value = data.FirstName;
                worksheet.Cells[row, 3].Value = data.LastName;
                worksheet.Cells[row, 4].Value = data.Email;
                worksheet.Cells[row, 5].Value = data.Phone;
                worksheet.Cells[row, 6].Value = data.NationalId;
                worksheet.Cells[row, 7].Value = data.UserTypeName;
                worksheet.Cells[row, 8].Value = data.CityName;
                worksheet.Cells[row, 9].Value = data.GradeName;
                worksheet.Cells[row, 10].Value = data.NationalityName;
                worksheet.Cells[row, 11].Value = data.HighSchoolName;
                worksheet.Cells[row, 12].Value = data.CertificateName;
                worksheet.Cells[row, 13].Value = data.HowDidYouKnowUsDisplay;
                worksheet.Cells[row, 14].Value = data.PrimaryMajor;
                worksheet.Cells[row, 15].Value = data.AllMajorsString;
                worksheet.Cells[row, 16].Value = data.RequestId > 0 ? data.RequestId.ToString() : "N/A";
                worksheet.Cells[row, 17].Value = data.StatusName;
                worksheet.Cells[row, 18].Value = data.ReasonDescription;
                worksheet.Cells[row, 19].Value = data.FollowUpCount;
                worksheet.Cells[row, 20].Value = data.LastFollowUpDate;
                worksheet.Cells[row, 20].Style.Numberformat.Format = "mm/dd/yyyy";
                worksheet.Cells[row, 21].Value = data.IsFollowUpOverdue ? "Yes" : "No";
                worksheet.Cells[row, 22].Value = data.IsRequestClosed ? "Yes" : "No";
                worksheet.Cells[row, 23].Value = data.PersonCreatedAt;
                worksheet.Cells[row, 23].Style.Numberformat.Format = "mm/dd/yyyy";
                worksheet.Cells[row, 24].Value = data.PersonCreatedByName;
                worksheet.Cells[row, 25].Value = data.RequestCreatedAt != DateTime.MinValue ? data.RequestCreatedAt : (DateTime?)null;
                worksheet.Cells[row, 25].Style.Numberformat.Format = "mm/dd/yyyy";
                worksheet.Cells[row, 26].Value = data.RequestCreatedByName;

                // Color coding for overdue follow-ups
                if (data.IsFollowUpOverdue)
                {
                    worksheet.Cells[row, 1, row, headers.Length].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1, row, headers.Length].Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
                }
                else if (data.RequiresFollowUp)
                {
                    worksheet.Cells[row, 1, row, headers.Length].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1, row, headers.Length].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                }
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            // Add borders
            var dataRange = worksheet.Cells[1, 1, completeData.Count + 1, headers.Length];
            dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        }

        private void CreateDetailsSheet(ExcelPackage package, List<CompletePersonRequestData> completeData)
        {
            var worksheet = package.Workbook.Worksheets.Add("Details");

            // Headers
            var headers = new string[]
            {
                "Person ID", "Full Name", "Email", "Phone", "National ID", "City", "Grade", "Nationality",
                "High School", "Certificate", "How Did You Know Us", "Primary Major", "All Major Interests",
                "Request ID", "Reason", "Comments", "Status", "Follow-up Count", "Last Follow-up Date",
                "Person Created Date", "Person Created By", "Person Updated Date", "Person Updated By",
                "Request Created Date", "Request Created By", "Request Updated Date", "Request Updated By"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            // Style headers
            var headerRange = worksheet.Cells[1, 1, 1, headers.Length];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(Color.DarkGreen);
            headerRange.Style.Font.Color.SetColor(Color.White);

            // Data rows
            for (int i = 0; i < completeData.Count; i++)
            {
                var data = completeData[i];
                int row = i + 2;

                worksheet.Cells[row, 1].Value = data.PersonId;
                worksheet.Cells[row, 2].Value = $"{data.FirstName} {data.LastName}";
                worksheet.Cells[row, 3].Value = data.Email;
                worksheet.Cells[row, 4].Value = data.Phone;
                worksheet.Cells[row, 5].Value = data.NationalId;
                worksheet.Cells[row, 6].Value = data.CityName;
                worksheet.Cells[row, 7].Value = data.GradeName;
                worksheet.Cells[row, 8].Value = data.NationalityName;
                worksheet.Cells[row, 9].Value = data.HighSchoolName;
                worksheet.Cells[row, 10].Value = data.CertificateName;
                worksheet.Cells[row, 11].Value = data.HowDidYouKnowUsDisplay;
                worksheet.Cells[row, 12].Value = data.PrimaryMajor;
                worksheet.Cells[row, 13].Value = data.AllMajorsString;
                worksheet.Cells[row, 14].Value = data.RequestId > 0 ? data.RequestId.ToString() : "N/A";
                worksheet.Cells[row, 15].Value = data.ReasonDescription;
                worksheet.Cells[row, 16].Value = data.Comments;
                worksheet.Cells[row, 17].Value = data.StatusName;
                worksheet.Cells[row, 18].Value = data.FollowUpCount;
                worksheet.Cells[row, 19].Value = data.LastFollowUpDate;
                worksheet.Cells[row, 19].Style.Numberformat.Format = "mm/dd/yyyy";
                worksheet.Cells[row, 20].Value = data.PersonCreatedAt;
                worksheet.Cells[row, 20].Style.Numberformat.Format = "mm/dd/yyyy hh:mm";
                worksheet.Cells[row, 21].Value = data.PersonCreatedByName;
                worksheet.Cells[row, 22].Value = data.PersonUpdatedAt;
                worksheet.Cells[row, 22].Style.Numberformat.Format = "mm/dd/yyyy hh:mm";
                worksheet.Cells[row, 23].Value = data.PersonUpdatedByName;
                worksheet.Cells[row, 24].Value = data.RequestCreatedAt != DateTime.MinValue ? data.RequestCreatedAt : (DateTime?)null;
                worksheet.Cells[row, 24].Style.Numberformat.Format = "mm/dd/yyyy hh:mm";
                worksheet.Cells[row, 25].Value = data.RequestCreatedByName;
                worksheet.Cells[row, 26].Value = data.RequestUpdatedAt != DateTime.MinValue ? data.RequestUpdatedAt : (DateTime?)null;
                worksheet.Cells[row, 26].Style.Numberformat.Format = "mm/dd/yyyy hh:mm";
                worksheet.Cells[row, 27].Value = data.RequestUpdatedByName;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            // Add borders
            var dataRange = worksheet.Cells[1, 1, completeData.Count + 1, headers.Length];
            dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        }

        private void CreateFollowUpSheet(ExcelPackage package, List<CompletePersonRequestData> completeData)
        {
            var worksheet = package.Workbook.Worksheets.Add("Follow-up Tracking");

            // Filter only records that require follow-up
            var followUpRecords = completeData.Where(p => p.RequiresFollowUp).ToList();

            // Headers
            var headers = new string[]
            {
                "Person ID", "Name", "Email", "Phone", "City", "Status", "Reason", "Follow-up Count",
                "Last Follow-up Date", "Days Since Last Follow-up", "Is Overdue", "Priority", "Comments"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            // Style headers
            var headerRange = worksheet.Cells[1, 1, 1, headers.Length];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(Color.DarkOrange);
            headerRange.Style.Font.Color.SetColor(Color.White);

            // Data rows
            for (int i = 0; i < followUpRecords.Count; i++)
            {
                var data = followUpRecords[i];
                int row = i + 2;

                var referenceDate = data.LastFollowUpDate ?? data.RequestCreatedAt;
                var daysSinceReference = referenceDate != DateTime.MinValue ? (DateTime.Now.Date - referenceDate.Date).Days : 0;

                worksheet.Cells[row, 1].Value = data.PersonId;
                worksheet.Cells[row, 2].Value = $"{data.FirstName} {data.LastName}";
                worksheet.Cells[row, 3].Value = data.Email;
                worksheet.Cells[row, 4].Value = data.Phone;
                worksheet.Cells[row, 5].Value = data.CityName;
                worksheet.Cells[row, 6].Value = data.StatusName;
                worksheet.Cells[row, 7].Value = data.ReasonDescription;
                worksheet.Cells[row, 8].Value = data.FollowUpCount;
                worksheet.Cells[row, 9].Value = data.LastFollowUpDate;
                worksheet.Cells[row, 9].Style.Numberformat.Format = "mm/dd/yyyy";
                worksheet.Cells[row, 10].Value = daysSinceReference;
                worksheet.Cells[row, 11].Value = data.IsFollowUpOverdue ? "Yes" : "No";
                worksheet.Cells[row, 12].Value = data.IsFollowUpOverdue ? "HIGH" : "NORMAL";
                worksheet.Cells[row, 13].Value = data.Comments;

                // Color coding based on priority
                if (data.IsFollowUpOverdue)
                {
                    worksheet.Cells[row, 1, row, headers.Length].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1, row, headers.Length].Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
                    worksheet.Cells[row, 12].Style.Font.Color.SetColor(Color.DarkRed);
                    worksheet.Cells[row, 12].Style.Font.Bold = true;
                }
                else
                {
                    worksheet.Cells[row, 1, row, headers.Length].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1, row, headers.Length].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                }
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            // Add borders
            if (followUpRecords.Any())
            {
                var dataRange = worksheet.Cells[1, 1, followUpRecords.Count + 1, headers.Length];
                dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }
        }

        private void CreateMajorInterestsSheet(ExcelPackage package, List<CompletePersonRequestData> completeData)
        {
            var worksheet = package.Workbook.Worksheets.Add("Major Interests");

            // Headers
            var headers = new string[]
            {
                "Person ID", "Full Name", "Email", "Phone", "City", "Grade", "Primary Major", "All Major Interests", "User Type"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            // Style headers
            var headerRange = worksheet.Cells[1, 1, 1, headers.Length];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(Color.DarkMagenta);
            headerRange.Style.Font.Color.SetColor(Color.White);

            // Get unique persons (avoid duplicates from multiple requests)
            var uniquePersons = completeData.GroupBy(p => p.PersonId).Select(g => g.First()).ToList();

            // Data rows
            for (int i = 0; i < uniquePersons.Count; i++)
            {
                var data = uniquePersons[i];
                int row = i + 2;

                worksheet.Cells[row, 1].Value = data.PersonId;
                worksheet.Cells[row, 2].Value = $"{data.FirstName} {data.LastName}";
                worksheet.Cells[row, 3].Value = data.Email;
                worksheet.Cells[row, 4].Value = data.Phone;
                worksheet.Cells[row, 5].Value = data.CityName;
                worksheet.Cells[row, 6].Value = data.GradeName;
                worksheet.Cells[row, 7].Value = data.PrimaryMajor;
                worksheet.Cells[row, 8].Value = data.AllMajorsString;
                worksheet.Cells[row, 9].Value = data.UserTypeName;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            // Add borders
            var dataRange = worksheet.Cells[1, 1, uniquePersons.Count + 1, headers.Length];
            dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
            dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        }

        private string GetUserTypeName(int userType)
        {
            return _userTypeOptions.TryGetValue(userType, out var name) ? name : "N/A";
        }
    }

    // Data model to hold complete person and request information
    public class CompletePersonRequestData
    {
        // Person properties
        public int PersonId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? NationalId { get; set; }
        public int UserType { get; set; }
        public string? UserTypeName { get; set; }
        public DateTime PersonCreatedAt { get; set; }
        public string? PersonCreatedByName { get; set; }
        public DateTime? PersonUpdatedAt { get; set; }
        public string? PersonUpdatedByName { get; set; }

        // Lookup properties
        public string? HighSchoolName { get; set; }
        public string? CertificateName { get; set; }
        public string? HowDidYouKnowUsDisplay { get; set; }
        public string? CityName { get; set; }
        public string? GradeName { get; set; }
        public string? NationalityName { get; set; }
        public List<string> MajorInterests { get; set; } = new List<string>();
        public string? PrimaryMajor { get; set; }
        public string? AllMajorsString { get; set; }

        // Request properties
        public int RequestId { get; set; }
        public int ReasonId { get; set; }
        public string? ReasonDescription { get; set; }
        public string? Comments { get; set; }
        public int FollowUpCount { get; set; }
        public DateTime? LastFollowUpDate { get; set; }
        public DateTime RequestCreatedAt { get; set; }
        public DateTime RequestUpdatedAt { get; set; }
        public string? RequestCreatedByName { get; set; }
        public string? RequestUpdatedByName { get; set; }
        public int StatusId { get; set; }
        public string? StatusName { get; set; }

        // Calculated properties
        public bool IsFollowUpOverdue { get; set; }
        public bool RequiresFollowUp { get; set; }
        public bool IsRequestClosed { get; set; }
    }
}