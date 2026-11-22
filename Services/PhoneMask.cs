namespace FestiveGuestAPI.Services;

/// <summary>
/// Phone numbers leave the API only masked, as 98******21. The one exception is the
/// admin-only GET api/admin/users/{id}/phone, for contacting a member.
/// </summary>
public static class PhoneMask
{
    public static string Apply(string? phone) =>
        string.IsNullOrEmpty(phone) || phone.Length < 4
            ? ""
            : $"{phone[..2]}{new string('*', phone.Length - 4)}{phone[^2..]}";
}
