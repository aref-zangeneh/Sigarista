using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class CostControlBoardConfiguration : IEntityTypeConfiguration<CostControlBoard>
{
    public void Configure(EntityTypeBuilder<CostControlBoard> builder)
    {
        builder.Property(board => board.Json).IsRequired();
        builder.Property(board => board.Version).IsRequired();
    }
}
