using UnityEngine;
using UnityEngine.InputSystem;
using CivilSim.Core;
using CivilSim.Buildings;

public class BuildingTestInput : MonoBehaviour
{
    [SerializeField] private BuildingData _testBuilding;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // 1키 : 배치 모드 시작
        if (kb.digit1Key.wasPressedThisFrame)
            GameManager.Instance.StartPlacing(_testBuilding);

        // 2키 : 철거 모드
        if (kb.digit2Key.wasPressedThisFrame)
            GameManager.Instance.StartRemoving();

        // Escape : 취소
        if (kb.escapeKey.wasPressedThisFrame)
            GameManager.Instance.CancelPlacing();
    }
}
