namespace Blackthunder.UniEngine.Core
{
    public static class ObjectTokenExtensions
    {
        public static bool IsValidAndSameTokenWith<T>(this T token, T other) where T : class
        {
            if (token == null) return false;

            return token == other;
        }
    }
}
