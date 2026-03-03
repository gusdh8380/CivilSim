using System;

namespace CivilSim.UI
{
    /// <summary>
    /// 설정/정책/리포트 패널의 동시 오픈을 방지하기 위한 코디네이터.
    /// 한 패널이 열리면 다른 패널은 이 이벤트를 받아 스스로 닫는다.
    /// </summary>
    public static class PanelOpenCoordinator
    {
        public static event Action<object> PanelOpened;

        public static void NotifyOpened(object panelOwner)
        {
            PanelOpened?.Invoke(panelOwner);
        }
    }
}
