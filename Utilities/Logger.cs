namespace TuneinCrew.Utilities
{
    internal class Logger
    {
        public static void Info(string message)
        {
            DateTime time = DateTime.Now;
            string customTime = time.ToString("HH:mm:ss");
            message = $"[{customTime}] {message}";

            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Info($"ERR: {message}");
        }

        public static void Warning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Info($"WARN: {message}");
        }
    }
}
