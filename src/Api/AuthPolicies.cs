namespace Microsoft.AspNetCore.Builder;

// The API's policy names. Written as a sentence, because a 403 says which policy was missing (SayWhatsMissing.cs).
public static class AuthPolicies
{
    public const string ReadCalendar = "the scope KickAssCalendar.Read";
    public const string ReadAllCalendars = "the app role KickAssCalendar.Read.All";
}
