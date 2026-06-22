using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Health_Guardian_AI.Migrations
{
    /// <inheritdoc />
    public partial class AddInFormPredictionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InFormPredictions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HPId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Messenge = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InFormPredictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InFormPredictions_HealthProfiles_HPId",
                        column: x => x.HPId,
                        principalTable: "HealthProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InFormPredictions_HPId",
                table: "InFormPredictions",
                column: "HPId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InFormPredictions");
        }
    }
}
