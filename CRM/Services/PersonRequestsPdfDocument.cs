using CRM.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRM.Services
{
    public class PersonRequestsPdfDocument : IDocument
    {
        private readonly List<PersonRequestViewModel> _personRequests;
        private readonly Dictionary<int, string> _userTypeOptions;

        public PersonRequestsPdfDocument(List<PersonRequestViewModel> personRequests, Dictionary<int, string> userTypeOptions = null)
        {
            _personRequests = personRequests;
            _userTypeOptions = userTypeOptions ?? new Dictionary<int, string>();
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4.Landscape());
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
        }

        void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Spacing(5);
                    column.Item().Text("Person Requests Report")
                        .FontSize(18)
                        .Bold()
                        .FontColor(Colors.Blue.Medium);

                    column.Item().Text($"Generated on: {DateTime.Now:MMMM dd, yyyy 'at' HH:mm}")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken2);

                    column.Item().Text($"Total Records: {_personRequests.Count}")
                        .FontSize(10)
                        .Bold()
                        .FontColor(Colors.Green.Darken2);
                });
            });
        }

        void ComposeContent(IContainer container)
        {
            container.PaddingVertical(10).Column(column =>
            {
                column.Spacing(15);

                if (!_personRequests.Any())
                {
                    column.Item().AlignCenter().Text("No records found")
                        .FontSize(14)
                        .FontColor(Colors.Grey.Medium);
                    return;
                }

                // Summary Section
                column.Item().Element(ComposeSummary);

                // Main Information Table
                column.Item().Text("Main Information").FontSize(12).Bold().FontColor(Colors.Blue.Medium);
                column.Item().Element(ComposeMainTable);

                // Additional Details Table
                column.Item().PaddingTop(20).Text("Additional Details").FontSize(12).Bold().FontColor(Colors.Blue.Medium);
                column.Item().Element(ComposeDetailsTable);
            });
        }

        void ComposeSummary(IContainer container)
        {
            var statusGroups = _personRequests
                .GroupBy(p => p.StatusName ?? "N/A")
                .ToDictionary(g => g.Key, g => g.Count());

            var userTypeGroups = _personRequests
                .GroupBy(p => GetUserTypeName(p.UserType))
                .ToDictionary(g => g.Key, g => g.Count());

            container.Background(Colors.Grey.Lighten4)
                .Padding(10)
                .Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text("Summary")
                        .FontSize(12)
                        .Bold()
                        .FontColor(Colors.Blue.Medium);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("By Status:")
                                .FontSize(10)
                                .Bold();

                            foreach (var status in statusGroups.Take(5)) // Limit to avoid overflow
                            {
                                col.Item().PaddingLeft(10).Text($"• {status.Key}: {status.Value}")
                                    .FontSize(9);
                            }
                        });

                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("By User Type:")
                                .FontSize(10)
                                .Bold();

                            foreach (var userType in userTypeGroups.Take(5)) // Limit to avoid overflow
                            {
                                col.Item().PaddingLeft(10).Text($"• {userType.Key}: {userType.Value}")
                                    .FontSize(9);
                            }
                        });
                    });
                });
        }

        void ComposeMainTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2); // Name
                    columns.RelativeColumn(3); // Email
                    columns.RelativeColumn(2); // Phone
                    columns.RelativeColumn(2); // National ID
                    columns.RelativeColumn(2); // User Type
                    columns.RelativeColumn(2); // Status
                    columns.RelativeColumn(2); // Created
                    columns.RelativeColumn(1); // Follow-ups
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Name").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Email").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Phone").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("National ID").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("User Type").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Status").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Created").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Follow-ups").FontColor(Colors.White).FontSize(9).Bold();
                });

                // Data rows
                foreach (var (person, index) in _personRequests.Select((p, i) => (p, i)))
                {
                    var backgroundColor = index % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                    table.Cell().Background(backgroundColor).Padding(4).Text($"{person.FirstName} ").FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(person.Email ?? "N/A").FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(person.Phone ?? "N/A").FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(person.NationalId ?? "N/A").FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(GetUserTypeName(person.UserType)).FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(person.StatusName ?? "N/A").FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(person.Person_CreatedAt.ToString("MM/dd/yyyy")).FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(person.FollowUpCount.ToString()).FontSize(8);
                }
            });
        }

        void ComposeDetailsTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    // columns.RelativeColumn(1); // Person ID
                    columns.RelativeColumn(2); // High School
                    columns.RelativeColumn(2); // Certificate
                                               // columns.RelativeColumn(2); // Major
                    columns.RelativeColumn(2); // How Did You Know Us
                    columns.RelativeColumn(2); // Created By
                    columns.RelativeColumn(2); // Last Follow-up
                    columns.RelativeColumn(3); // Comments
                });

                // Header
                table.Header(header =>
                {
                    // header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Person ID").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("High School").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Certificate").FontColor(Colors.White).FontSize(9).Bold();
                    //  header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Major").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("How Did You Know Us").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Created By").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Last Follow-up").FontColor(Colors.White).FontSize(9).Bold();
                    header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Comments").FontColor(Colors.White).FontSize(9).Bold();
                });

                // Data rows
                foreach (var (person, index) in _personRequests.Select((p, i) => (p, i)))
                {
                    var backgroundColor = index % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                    //
                    //table.Cell().Background(backgroundColor).Padding(4).Text(person.PersonID.ToString()).FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(TruncateText(person.HighSchoolName ?? "N/A", 20)).FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(TruncateText(person.CertificateName ?? "N/A", 20)).FontSize(8);
                    //table.Cell().Background(backgroundColor).Padding(4).Text(TruncateText(person.MajorName ?? "N/A", 20)).FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(TruncateText(person.HowDidYouKnowUsName ?? "N/A", 20)).FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(TruncateText(person.Person_CreatedByName ?? "N/A", 15)).FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(person.LastFollowUpDate?.ToString("MM/dd/yyyy") ?? "N/A").FontSize(8);
                    table.Cell().Background(backgroundColor).Padding(4).Text(TruncateText(person.Comments ?? "N/A", 30)).FontSize(8);
                }
            });
        }

        void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        }

        private string GetUserTypeName(int userType)
        {
            return _userTypeOptions.TryGetValue(userType, out var name) ? name : "N/A";
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - 3) + "...";
        }
    }
}