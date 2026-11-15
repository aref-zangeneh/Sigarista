using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

/// <summary>Membership of a contact in a contact group (unique pair).</summary>
public class SmsContactGroupMember : BaseEntity
{
    public SmsContactGroupMember(Guid groupId, Guid contactId)
    {
        GroupId = groupId;
        ContactId = contactId;
    }

    private SmsContactGroupMember()
    {
    }

    public Guid GroupId { get; private set; }

    public Guid ContactId { get; private set; }
}
