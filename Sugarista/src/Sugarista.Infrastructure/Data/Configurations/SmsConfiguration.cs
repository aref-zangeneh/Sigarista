using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sugarista.Domain.Entities;

namespace Sugarista.Infrastructure.Data.Configurations;

public class SmsConfiguration :
    IEntityTypeConfiguration<SmsProvider>,
    IEntityTypeConfiguration<SmsSenderLine>,
    IEntityTypeConfiguration<SmsMessage>,
    IEntityTypeConfiguration<SmsCampaign>,
    IEntityTypeConfiguration<SmsContact>,
    IEntityTypeConfiguration<SmsContactGroup>,
    IEntityTypeConfiguration<SmsContactGroupMember>,
    IEntityTypeConfiguration<SmsTemplate>,
    IEntityTypeConfiguration<SmsTemplateProviderMapping>,
    IEntityTypeConfiguration<SmsOptOut>,
    IEntityTypeConfiguration<SmsProviderLog>
{
    public void Configure(EntityTypeBuilder<SmsProvider> builder)
    {
        builder.Property(provider => provider.Name).HasMaxLength(120).IsRequired();
        builder.Property(provider => provider.BaseUrl).HasMaxLength(300);
        builder.Property(provider => provider.EncryptedApiKey).HasMaxLength(2048);
        builder.Property(provider => provider.EncryptedUsername).HasMaxLength(512);
        builder.Property(provider => provider.EncryptedPassword).HasMaxLength(512);
        builder.Property(provider => provider.ExtraSettingsJson).HasMaxLength(2000);
        builder.Property(provider => provider.LastConnectionError).HasMaxLength(520);
        builder.HasIndex(provider => provider.ProviderType);
        builder.HasMany(provider => provider.SenderLines)
            .WithOne()
            .HasForeignKey(line => line.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<SmsSenderLine> builder)
    {
        builder.Property(line => line.Number).HasMaxLength(20).IsRequired();
        builder.Property(line => line.Label).HasMaxLength(80);
    }

    public void Configure(EntityTypeBuilder<SmsMessage> builder)
    {
        builder.Property(message => message.Mobile).HasMaxLength(16).IsRequired();
        builder.Property(message => message.Body).HasMaxLength(1400).IsRequired();
        builder.Property(message => message.ProviderMessageId).HasMaxLength(120);
        builder.Property(message => message.ProviderResponse).HasMaxLength(500);
        builder.Property(message => message.ErrorMessage).HasMaxLength(520);
        builder.Property(message => message.RequestedBy).HasMaxLength(120);
        // The hot query paths: history by mobile / status / campaign / date.
        builder.HasIndex(message => message.Mobile);
        builder.HasIndex(message => new { message.Status, message.RequestedAt });
        builder.HasIndex(message => message.CampaignId);
        builder.HasIndex(message => message.ProviderMessageId);
    }

    public void Configure(EntityTypeBuilder<SmsCampaign> builder)
    {
        builder.Property(campaign => campaign.Title).HasMaxLength(160).IsRequired();
        builder.Property(campaign => campaign.Body).HasMaxLength(1400).IsRequired();
        builder.HasIndex(campaign => campaign.Status);
    }

    public void Configure(EntityTypeBuilder<SmsContact> builder)
    {
        builder.Property(contact => contact.FirstName).HasMaxLength(80);
        builder.Property(contact => contact.LastName).HasMaxLength(80);
        builder.Property(contact => contact.Mobile).HasMaxLength(16).IsRequired();
        builder.Property(contact => contact.Email).HasMaxLength(160);
        builder.Property(contact => contact.Notes).HasMaxLength(500);
        // One row per phone number — deduplication at the data layer.
        builder.HasIndex(contact => contact.Mobile).IsUnique();
        builder.HasIndex(contact => new { contact.LastName, contact.FirstName });
    }

    public void Configure(EntityTypeBuilder<SmsContactGroup> builder)
    {
        builder.Property(group => group.Name).HasMaxLength(120).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(400);
        builder.HasIndex(group => group.Name).IsUnique();
    }

    public void Configure(EntityTypeBuilder<SmsContactGroupMember> builder)
    {
        builder.HasIndex(member => new { member.GroupId, member.ContactId }).IsUnique();
        builder.HasIndex(member => member.ContactId);
    }

    public void Configure(EntityTypeBuilder<SmsTemplate> builder)
    {
        builder.Property(template => template.Name).HasMaxLength(120).IsRequired();
        builder.Property(template => template.Description).HasMaxLength(400);
        builder.Property(template => template.Body).HasMaxLength(1400).IsRequired();
        builder.HasMany(template => template.ProviderMappings)
            .WithOne()
            .HasForeignKey(mapping => mapping.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<SmsTemplateProviderMapping> builder)
    {
        builder.Property(mapping => mapping.ProviderTemplateId).HasMaxLength(200).IsRequired();
        builder.HasIndex(mapping => new { mapping.TemplateId, mapping.ProviderType }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<SmsOptOut> builder)
    {
        builder.Property(optOut => optOut.Mobile).HasMaxLength(16).IsRequired();
        builder.Property(optOut => optOut.Reason).HasMaxLength(300);
        builder.HasIndex(optOut => optOut.Mobile).IsUnique();
    }

    public void Configure(EntityTypeBuilder<SmsProviderLog> builder)
    {
        builder.Property(log => log.Action).HasMaxLength(60).IsRequired();
        builder.Property(log => log.Detail).HasMaxLength(520);
        builder.HasIndex(log => new { log.ProviderId, log.Action });
        builder.HasIndex(log => log.CreatedAt);
    }
}
