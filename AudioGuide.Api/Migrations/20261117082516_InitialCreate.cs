using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AudioGuide.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatCache",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AnswerJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatCache", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NativeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TranslatorCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TtsVoice = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                    table.UniqueConstraint("AK_Languages_Code", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Pois",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    TriggerRadiusM = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImageUrls = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EntryFeeVnd = table.Column<int>(type: "int", nullable: false),
                    VisitMinutes = table.Column<int>(type: "int", nullable: true),
                    Amenities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tips = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pois", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccessSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CodeExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccessExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessSessions_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UiTranslations",
                columns: table => new
                {
                    Lang = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StringsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiTranslations", x => x.Lang);
                    table.ForeignKey(
                        name: "FK_UiTranslations_Languages_Lang",
                        column: x => x.Lang,
                        principalTable: "Languages",
                        principalColumn: "Code");
                });

            migrationBuilder.CreateTable(
                name: "PoiLocalizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoiId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AudioUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TranslatedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Tips = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TipsHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoiLocalizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoiLocalizations_Languages_Lang",
                        column: x => x.Lang,
                        principalTable: "Languages",
                        principalColumn: "Code");
                    table.ForeignKey(
                        name: "FK_PoiLocalizations_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PoiOpeningHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoiId = table.Column<int>(type: "int", nullable: false),
                    Day = table.Column<int>(type: "int", nullable: false),
                    Open = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Close = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoiOpeningHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoiOpeningHours_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TourPois",
                columns: table => new
                {
                    TourId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    PoiId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TourPois", x => new { x.TourId, x.Position });
                    table.ForeignKey(
                        name: "FK_TourPois_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TourPois_Tours_TourId",
                        column: x => x.TourId,
                        principalTable: "Tours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TourTranslations",
                columns: table => new
                {
                    TourId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TourTranslations", x => new { x.TourId, x.Lang });
                    table.ForeignKey(
                        name: "FK_TourTranslations_Languages_Lang",
                        column: x => x.Lang,
                        principalTable: "Languages",
                        principalColumn: "Code");
                    table.ForeignKey(
                        name: "FK_TourTranslations_Tours_TourId",
                        column: x => x.TourId,
                        principalTable: "Tours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: true),
                    Lang = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UsedLlm = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatLogs_AccessSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AccessSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatLogs_Languages_Lang",
                        column: x => x.Lang,
                        principalTable: "Languages",
                        principalColumn: "Code");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProviderRef = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_AccessSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AccessSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VisitEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: true),
                    PoiId = table.Column<int>(type: "int", nullable: false),
                    Lang = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitEvents_AccessSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AccessSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VisitEvents_Pois_PoiId",
                        column: x => x.PoiId,
                        principalTable: "Pois",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessSessions_Code",
                table: "AccessSessions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessSessions_CreatedById",
                table: "AccessSessions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AccessSessions_Status_CreatedAt",
                table: "AccessSessions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatCache_ExpiresAt",
                table: "ChatCache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatLogs_Lang",
                table: "ChatLogs",
                column: "Lang");

            migrationBuilder.CreateIndex(
                name: "IX_ChatLogs_SessionId_CreatedAt",
                table: "ChatLogs",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Code",
                table: "Languages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SessionId",
                table: "Payments",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PoiLocalizations_Lang",
                table: "PoiLocalizations",
                column: "Lang");

            migrationBuilder.CreateIndex(
                name: "IX_PoiLocalizations_PoiId_Lang",
                table: "PoiLocalizations",
                columns: new[] { "PoiId", "Lang" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoiLocalizations_Status",
                table: "PoiLocalizations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PoiOpeningHours_PoiId",
                table: "PoiOpeningHours",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_Pois_Code",
                table: "Pois",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pois_IsActive_SortOrder",
                table: "Pois",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TourPois_PoiId",
                table: "TourPois",
                column: "PoiId");

            migrationBuilder.CreateIndex(
                name: "IX_Tours_Code",
                table: "Tours",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TourTranslations_Lang",
                table: "TourTranslations",
                column: "Lang");

            migrationBuilder.CreateIndex(
                name: "IX_VisitEvents_PoiId_CreatedAt",
                table: "VisitEvents",
                columns: new[] { "PoiId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitEvents_SessionId",
                table: "VisitEvents",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ChatCache");

            migrationBuilder.DropTable(
                name: "ChatLogs");

            migrationBuilder.DropTable(
                name: "KnowledgeArticles");

            migrationBuilder.DropTable(
                name: "MediaFiles");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PoiLocalizations");

            migrationBuilder.DropTable(
                name: "PoiOpeningHours");

            migrationBuilder.DropTable(
                name: "TourPois");

            migrationBuilder.DropTable(
                name: "TourTranslations");

            migrationBuilder.DropTable(
                name: "UiTranslations");

            migrationBuilder.DropTable(
                name: "VisitEvents");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Tours");

            migrationBuilder.DropTable(
                name: "Languages");

            migrationBuilder.DropTable(
                name: "AccessSessions");

            migrationBuilder.DropTable(
                name: "Pois");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
