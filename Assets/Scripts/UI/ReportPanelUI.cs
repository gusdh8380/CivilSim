using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using CivilSim.Core;
using CivilSim.Economy;
using CivilSim.Population;


public class ReportPanelUI : MonoBehaviour
{
    [Header("패널 루트 (자식 오브젝트)")]
    [SerializeField] private GameObject _panel;

    [Header("열기/닫기 버튼 (미할당 시 자동 탐색)")]
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _closeButton;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
