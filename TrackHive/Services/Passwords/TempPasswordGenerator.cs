namespace TrackHive.Services.Passwords
{
    public static class TempPasswordGenerator
    {
        // Simple: 10 chars; you can harden later
        public static string Generate(int length = 10)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
            var rnd = new Random();
            return new string(Enumerable.Range(0, length).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
        }
    }
}
