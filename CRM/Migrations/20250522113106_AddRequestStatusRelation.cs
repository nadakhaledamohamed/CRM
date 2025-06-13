using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestStatusRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Academic_Setting",
                columns: table => new
                {
                    Academic_Setting_ID = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    SemsterName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, comment: "1 for fall ,2 for spring "),
                    NumberOf_Interests = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Academic_Setting", x => x.Academic_Setting_ID);
                });

            migrationBuilder.CreateTable(
                name: "LookUp_HighSchool_Cert",
                columns: table => new
                {
                    Certificate_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    certificate_Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookUp_HighSchool_Cert_1", x => x.Certificate_ID);
                });

            migrationBuilder.CreateTable(
                name: "LookUp_HighSchools",
                columns: table => new
                {
                    HighSchool_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HighSchoolName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookUp_HighSchools", x => x.HighSchool_ID);
                });

            migrationBuilder.CreateTable(
                name: "LookUp_HowDidYouKnowUs",
                columns: table => new
                {
                    HowDidYouKnowUs_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HowDidYouKnowUs = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LookUp_HowDidYouKnowUs", x => x.HowDidYouKnowUs_ID);
                });

            migrationBuilder.CreateTable(
                name: "Lookup_Major",
                columns: table => new
                {
                    MajorID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Major_Interest = table.Column<string>(type: "nchar(150)", fixedLength: true, maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lookup_Major", x => x.MajorID);
                });

            migrationBuilder.CreateTable(
                name: "Lookup_Roles",
                columns: table => new
                {
                    RoleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nchar(10)", fixedLength: true, maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lookup_Roles", x => x.RoleID);
                });

            migrationBuilder.CreateTable(
                name: "LookUp_StatusTypes",
                columns: table => new
                {
                    StatusID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatusName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    RequireFollowUp = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__StatusTy__C8EE2043CC4C0C0E", x => x.StatusID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastLogin = table.Column<DateTime>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nchar(250)", fixedLength: true, maxLength: 250, nullable: true),
                    UserCode = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nchar(250)", fixedLength: true, maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__1788CCACD67BA816", x => x.UserID);
                    table.ForeignKey(
                        name: "FK_Users_Roles",
                        column: x => x.RoleId,
                        principalTable: "Lookup_Roles",
                        principalColumn: "RoleID");
                });

            migrationBuilder.CreateTable(
                name: "Person",
                columns: table => new
                {
                    PersonID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NationalID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Certificate_ID = table.Column<int>(type: "int", nullable: false),
                    HighSchool_ID = table.Column<int>(type: "int", nullable: false),
                    HowDidYouKnowUs_ID = table.Column<int>(type: "int", nullable: false),
                    UserType = table.Column<int>(type: "int", nullable: false, comment: "1 for Applicant & 2 for Gurdian "),
                    CreatedBy_Code = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    UpdatedBy_Code = table.Column<int>(type: "int", nullable: true),
                    Major_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Applican__39AE914807969DE7", x => x.PersonID);
                    table.ForeignKey(
                        name: "FK_Person_LookUp_HighSchool_Cert1",
                        column: x => x.Certificate_ID,
                        principalTable: "LookUp_HighSchool_Cert",
                        principalColumn: "Certificate_ID");
                    table.ForeignKey(
                        name: "FK_Person_LookUp_HighSchools1",
                        column: x => x.HighSchool_ID,
                        principalTable: "LookUp_HighSchools",
                        principalColumn: "HighSchool_ID");
                    table.ForeignKey(
                        name: "FK_Person_LookUp_HowDidYouKnowUs",
                        column: x => x.HowDidYouKnowUs_ID,
                        principalTable: "LookUp_HowDidYouKnowUs",
                        principalColumn: "HowDidYouKnowUs_ID");
                    table.ForeignKey(
                        name: "FK_Person_Lookup_Major",
                        column: x => x.Major_ID,
                        principalTable: "Lookup_Major",
                        principalColumn: "MajorID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Person_Users",
                        column: x => x.UpdatedBy_Code,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_Person_Users1",
                        column: x => x.CreatedBy_Code,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    RequestID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Comments = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedBy_Code = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    UpdatedBy_Code = table.Column<int>(type: "int", nullable: true),
                    LastFollowUpDate = table.Column<DateTime>(type: "datetime", nullable: true),
                    FollowUpCount = table.Column<int>(type: "int", nullable: false),
                    PersonID = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Requests__33A8519A39A60AAD", x => x.RequestID);
                    table.ForeignKey(
                        name: "FK_Requests_LookupStatus",
                        column: x => x.StatusId,
                        principalTable: "LookUp_StatusTypes",
                        principalColumn: "StatusID");
                    table.ForeignKey(
                        name: "FK_Requests_Person",
                        column: x => x.PersonID,
                        principalTable: "Person",
                        principalColumn: "PersonID");
                    table.ForeignKey(
                        name: "FK_Requests_Users",
                        column: x => x.CreatedBy_Code,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "FK_Requests_Users1",
                        column: x => x.UpdatedBy_Code,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "StatusHistory",
                columns: table => new
                {
                    HistoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestID = table.Column<int>(type: "int", nullable: false),
                    StatusID = table.Column<int>(type: "int", nullable: false),
                    ChangeReason = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    IsCurrent_Status = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    UpdatedBy_Code = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__StatusHi__4D7B4ADDB00E26D6", x => x.HistoryID);
                    table.ForeignKey(
                        name: "FK_StatusHistory_LookUp_StatusTypes",
                        column: x => x.StatusID,
                        principalTable: "LookUp_StatusTypes",
                        principalColumn: "StatusID");
                    table.ForeignKey(
                        name: "FK_StatusHistory_Requests",
                        column: x => x.RequestID,
                        principalTable: "Requests",
                        principalColumn: "RequestID");
                    table.ForeignKey(
                        name: "FK_StatusHistory_Users",
                        column: x => x.UpdatedBy_Code,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "UQ__StatusTy__05E7698A1B6E7D5A",
                table: "LookUp_StatusTypes",
                column: "StatusName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Person_Certificate_ID",
                table: "Person",
                column: "Certificate_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Person_CreatedBy_Code",
                table: "Person",
                column: "CreatedBy_Code");

            migrationBuilder.CreateIndex(
                name: "IX_Person_HighSchool_ID",
                table: "Person",
                column: "HighSchool_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Person_HowDidYouKnowUs_ID",
                table: "Person",
                column: "HowDidYouKnowUs_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Person_Major_ID",
                table: "Person",
                column: "Major_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Person_UpdatedBy_Code",
                table: "Person",
                column: "UpdatedBy_Code");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CreatedBy_Code",
                table: "Requests",
                column: "CreatedBy_Code");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_PersonID",
                table: "Requests",
                column: "PersonID");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_StatusId",
                table: "Requests",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_UpdatedBy_Code",
                table: "Requests",
                column: "UpdatedBy_Code");

            migrationBuilder.CreateIndex(
                name: "IX_StatusHistory_RequestID",
                table: "StatusHistory",
                column: "RequestID");

            migrationBuilder.CreateIndex(
                name: "IX_StatusHistory_StatusID",
                table: "StatusHistory",
                column: "StatusID");

            migrationBuilder.CreateIndex(
                name: "IX_StatusHistory_UpdatedBy_Code",
                table: "StatusHistory",
                column: "UpdatedBy_Code");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UQ__Users__536C85E4737850FD",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Users__A9D10534CE0E781A",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Academic_Setting");

            migrationBuilder.DropTable(
                name: "StatusHistory");

            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.DropTable(
                name: "LookUp_StatusTypes");

            migrationBuilder.DropTable(
                name: "Person");

            migrationBuilder.DropTable(
                name: "LookUp_HighSchool_Cert");

            migrationBuilder.DropTable(
                name: "LookUp_HighSchools");

            migrationBuilder.DropTable(
                name: "LookUp_HowDidYouKnowUs");

            migrationBuilder.DropTable(
                name: "Lookup_Major");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Lookup_Roles");
        }
    }
}
