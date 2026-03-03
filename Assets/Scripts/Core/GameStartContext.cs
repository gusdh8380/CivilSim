namespace CivilSim.Core
{
    /// <summary>
    /// Entry 씬에서 Game Play 씬으로 전달할 시작 요청 컨텍스트.
    /// </summary>
    public static class GameStartContext
    {
        private static string _pendingLoadSlot;

        public static void RequestNewGame()
        {
            _pendingLoadSlot = null;
        }

        public static void RequestLoad(string slotName)
        {
            _pendingLoadSlot = string.IsNullOrWhiteSpace(slotName) ? null : slotName.Trim().ToLowerInvariant();
        }

        public static bool ConsumePendingLoadSlot(out string slotName)
        {
            slotName = _pendingLoadSlot;
            _pendingLoadSlot = null;
            return !string.IsNullOrWhiteSpace(slotName);
        }
    }
}
