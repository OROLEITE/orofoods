using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orofoods.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1FoundationAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AspNetRoles" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
                    "Name" TEXT NULL,
                    "NormalizedName" TEXT NULL,
                    "ConcurrencyStamp" TEXT NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "RoleNameIndex"
                ON "AspNetRoles" ("NormalizedName");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AspNetUsers" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
                    "UserName" TEXT NULL,
                    "NormalizedUserName" TEXT NULL,
                    "Email" TEXT NULL,
                    "NormalizedEmail" TEXT NULL,
                    "EmailConfirmed" INTEGER NOT NULL,
                    "PasswordHash" TEXT NULL,
                    "SecurityStamp" TEXT NULL,
                    "ConcurrencyStamp" TEXT NULL,
                    "PhoneNumber" TEXT NULL,
                    "PhoneNumberConfirmed" INTEGER NOT NULL,
                    "TwoFactorEnabled" INTEGER NOT NULL,
                    "LockoutEnd" TEXT NULL,
                    "LockoutEnabled" INTEGER NOT NULL,
                    "AccessFailedCount" INTEGER NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "UserNameIndex"
                ON "AspNetUsers" ("NormalizedUserName");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "EmailIndex"
                ON "AspNetUsers" ("NormalizedEmail");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AspNetRoleClaims" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
                    "RoleId" TEXT NOT NULL,
                    "ClaimType" TEXT NULL,
                    "ClaimValue" TEXT NULL,
                    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId"
                        FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_AspNetRoleClaims_RoleId"
                ON "AspNetRoleClaims" ("RoleId");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AspNetUserClaims" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
                    "UserId" TEXT NOT NULL,
                    "ClaimType" TEXT NULL,
                    "ClaimValue" TEXT NULL,
                    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_AspNetUserClaims_UserId"
                ON "AspNetUserClaims" ("UserId");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AspNetUserLogins" (
                    "LoginProvider" TEXT NOT NULL,
                    "ProviderKey" TEXT NOT NULL,
                    "ProviderDisplayName" TEXT NULL,
                    "UserId" TEXT NOT NULL,
                    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
                    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_AspNetUserLogins_UserId"
                ON "AspNetUserLogins" ("UserId");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AspNetUserRoles" (
                    "UserId" TEXT NOT NULL,
                    "RoleId" TEXT NOT NULL,
                    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
                    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId"
                        FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_AspNetUserRoles_RoleId"
                ON "AspNetUserRoles" ("RoleId");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AspNetUserTokens" (
                    "UserId" TEXT NOT NULL,
                    "LoginProvider" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "Value" TEXT NULL,
                    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
                    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalInformation",
                table: "Products",
                type: "TEXT",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApproximateShelfLife",
                table: "Products",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Products",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ingredients",
                table: "Products",
                type: "TEXT",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPromotional",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProductCategoryId",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PromotionalPrice",
                table: "Products",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageInformation",
                table: "Products",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StorageTemperature",
                table: "Products",
                type: "TEXT",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Products",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UnitsPerPackage",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "Products",
                type: "TEXT",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Orders",
                type: "TEXT",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Freight",
                table: "Orders",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PaymentTermId",
                table: "Orders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Orders",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductNameSnapshot",
                table: "OrderItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SkuSnapshot",
                table: "OrderItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "OrderItems",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Customers",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Customers",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PriceTableId",
                table: "Customers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleDocument",
                table: "Customers",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleName",
                table: "Customers",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SalesRepresentativeId",
                table: "Customers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateRegistration",
                table: "Customers",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WhatsApp",
                table: "Customers",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Complement",
                table: "CustomerAddresses",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "CustomerAddresses",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CustomerAddresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "CustomerAddresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Number",
                table: "CustomerAddresses",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "CustomerAddresses",
                type: "TEXT",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SalesRepresentativeId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    AltText = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesRepresentatives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesRepresentatives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPaymentTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentTermId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentTerms_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentTerms_PaymentTerms_PaymentTermId",
                        column: x => x.PaymentTermId,
                        principalTable: "PaymentTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceTableItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PriceTableId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    PromotionalPrice = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceTableItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceTableItems_PriceTables_PriceTableId",
                        column: x => x.PriceTableId,
                        principalTable: "PriceTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceTableItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO ProductCategories (Name, Slug, SortOrder, IsActive)
                SELECT 'Migrado', 'migrado', 0, 1
                WHERE NOT EXISTS (SELECT 1 FROM ProductCategories WHERE Slug = 'migrado');
                """);

            migrationBuilder.Sql("""
                INSERT INTO ProductCategories (Name, Slug, SortOrder, IsActive)
                SELECT DISTINCT Category, 'categoria-' || Id, 0, 1
                FROM (
                    SELECT MIN(Id) AS Id, Category
                    FROM Products
                    WHERE Category IS NOT NULL AND trim(Category) <> ''
                    GROUP BY Category
                ) AS LegacyCategories
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM ProductCategories pc
                    WHERE pc.Name = LegacyCategories.Category);
                """);

            migrationBuilder.Sql("""
                UPDATE Products
                SET ProductCategoryId = COALESCE(
                    (SELECT pc.Id FROM ProductCategories pc WHERE pc.Name = Products.Category LIMIT 1),
                    (SELECT pc.Id FROM ProductCategories pc WHERE pc.Slug = 'migrado' LIMIT 1));
                """);

            migrationBuilder.Sql("""
                UPDATE Products
                SET Brand = 'Orofoods',
                    IsActive = 1,
                    Unit = 'caixa'
                WHERE Brand = '' OR Unit = '' OR IsActive = 0;
                """);

            migrationBuilder.Sql("""
                INSERT INTO PriceTables (Name, IsActive)
                SELECT DISTINCT PriceTableName, 1
                FROM Customers
                WHERE PriceTableName IS NOT NULL AND trim(PriceTableName) <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM PriceTables pt
                      WHERE pt.Name = Customers.PriceTableName);
                """);

            migrationBuilder.Sql("""
                UPDATE Customers
                SET PriceTableId = (
                    SELECT pt.Id
                    FROM PriceTables pt
                    WHERE pt.Name = Customers.PriceTableName
                    LIMIT 1),
                    IsActive = 1,
                    CreatedAt = CURRENT_TIMESTAMP
                WHERE IsActive = 0;
                """);

            migrationBuilder.Sql("""
                INSERT INTO PaymentTerms (Name, SortOrder, IsActive)
                SELECT DISTINCT PaymentTerms, 0, 1
                FROM Customers
                WHERE PaymentTerms IS NOT NULL AND trim(PaymentTerms) <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM PaymentTerms pt
                      WHERE pt.Name = Customers.PaymentTerms);
                """);

            migrationBuilder.Sql("""
                INSERT INTO CustomerPaymentTerms (CustomerId, PaymentTermId, IsActive)
                SELECT c.Id, pt.Id, 1
                FROM Customers c
                JOIN PaymentTerms pt ON pt.Name = c.PaymentTerms
                WHERE c.PaymentTerms IS NOT NULL AND trim(c.PaymentTerms) <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM CustomerPaymentTerms cpt
                      WHERE cpt.CustomerId = c.Id AND cpt.PaymentTermId = pt.Id);
                """);

            migrationBuilder.Sql("""
                INSERT INTO PriceTableItems (PriceTableId, ProductId, Price, PromotionalPrice)
                SELECT c.PriceTableId, cp.ProductId, cp.Price, NULL
                FROM CustomerPrices cp
                JOIN Customers c ON c.Id = cp.CustomerId
                WHERE c.PriceTableId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM PriceTableItems pti
                      WHERE pti.PriceTableId = c.PriceTableId AND pti.ProductId = cp.ProductId);
                """);

            migrationBuilder.Sql("""
                UPDATE CustomerAddresses
                SET IsPrimary = 1,
                    IsActive = 1
                WHERE IsPrimary = 0 AND IsActive = 0;
                """);

            migrationBuilder.Sql("""
                INSERT INTO AspNetUsers (
                    Id, UserName, NormalizedUserName, Email, NormalizedEmail,
                    EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                    PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
                    IsActive)
                SELECT 'seed-system-user',
                       'sistema@orofoods.local',
                       'SISTEMA@OROFOODS.LOCAL',
                       'sistema@orofoods.local',
                       'SISTEMA@OROFOODS.LOCAL',
                       1, '', 'migration', 'migration',
                       0, 0, 0, 0,
                       1
                WHERE NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Id = 'seed-system-user');
                """);

            migrationBuilder.Sql("""
                UPDATE Orders
                SET CreatedByUserId = 'seed-system-user',
                    Subtotal = CASE WHEN Subtotal = 0 THEN Total ELSE Subtotal END
                WHERE CreatedByUserId = '';
                """);

            migrationBuilder.Sql("""
                UPDATE OrderItems
                SET ProductNameSnapshot = COALESCE(
                        (SELECT Name FROM Products p WHERE p.Id = OrderItems.ProductId),
                        ProductNameSnapshot),
                    SkuSnapshot = COALESCE(
                        (SELECT Sku FROM Products p WHERE p.Id = OrderItems.ProductId),
                        SkuSnapshot),
                    Subtotal = CASE WHEN Subtotal = 0 THEN Quantity * UnitPrice ELSE Subtotal END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductCategoryId",
                table: "Products",
                column: "ProductCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedByUserId",
                table: "Orders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentTermId",
                table: "Orders",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Cnpj",
                table: "Customers",
                column: "Cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PriceTableId",
                table: "Customers",
                column: "PriceTableId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_SalesRepresentativeId",
                table: "Customers",
                column: "SalesRepresentativeId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CustomerId",
                table: "AspNetUsers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SalesRepresentativeId",
                table: "AspNetUsers",
                column: "SalesRepresentativeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentTerms_CustomerId_PaymentTermId",
                table: "CustomerPaymentTerms",
                columns: new[] { "CustomerId", "PaymentTermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentTerms_PaymentTermId",
                table: "CustomerPaymentTerms",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceTableItems_PriceTableId_ProductId",
                table: "PriceTableItems",
                columns: new[] { "PriceTableId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceTableItems_ProductId",
                table: "PriceTableItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_Slug",
                table: "ProductCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Customers_CustomerId",
                table: "AspNetUsers",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_SalesRepresentatives_SalesRepresentativeId",
                table: "AspNetUsers",
                column: "SalesRepresentativeId",
                principalTable: "SalesRepresentatives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_PriceTables_PriceTableId",
                table: "Customers",
                column: "PriceTableId",
                principalTable: "PriceTables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_SalesRepresentatives_SalesRepresentativeId",
                table: "Customers",
                column: "SalesRepresentativeId",
                principalTable: "SalesRepresentatives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_CreatedByUserId",
                table: "Orders",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_PaymentTerms_PaymentTermId",
                table: "Orders",
                column: "PaymentTermId",
                principalTable: "PaymentTerms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ProductCategories_ProductCategoryId",
                table: "Products",
                column: "ProductCategoryId",
                principalTable: "ProductCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Customers_CustomerId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_SalesRepresentatives_SalesRepresentativeId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_PriceTables_PriceTableId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_SalesRepresentatives_SalesRepresentativeId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_CreatedByUserId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_PaymentTerms_PaymentTermId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_ProductCategories_ProductCategoryId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "CustomerPaymentTerms");

            migrationBuilder.DropTable(
                name: "PriceTableItems");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "SalesRepresentatives");

            migrationBuilder.DropTable(
                name: "PaymentTerms");

            migrationBuilder.DropTable(
                name: "PriceTables");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductCategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CreatedByUserId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentTermId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Cnpj",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_PriceTableId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_SalesRepresentativeId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CustomerId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SalesRepresentativeId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AdditionalInformation",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ApproximateShelfLife",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Ingredients",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsPromotional",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductCategoryId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PromotionalPrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StorageInformation",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StorageTemperature",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitsPerPackage",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Freight",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProductNameSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SkuSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PriceTableId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ResponsibleDocument",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ResponsibleName",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SalesRepresentativeId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "StateRegistration",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "WhatsApp",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Complement",
                table: "CustomerAddresses");

            migrationBuilder.DropColumn(
                name: "District",
                table: "CustomerAddresses");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CustomerAddresses");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "CustomerAddresses");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "CustomerAddresses");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "CustomerAddresses");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SalesRepresentativeId",
                table: "AspNetUsers");
        }
    }
}
