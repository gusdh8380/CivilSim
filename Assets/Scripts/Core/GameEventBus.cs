using System;
using System.Collections.Generic;
using UnityEngine;

namespace CivilSim.Core
{
    /// <summary>
    /// 전역 이벤트 버스. 시스템 간 직접 참조 없이 통신한다.
    /// 사용법:
    ///   구독: GameEventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
    ///   발행: GameEventBus.Publish(new MoneyChangedEvent { NewAmount = 1000, Delta = 100 });
    ///   해제: GameEventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);
    /// </summary>
    public static class GameEventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }
            list.Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list))
                list.Remove(handler);
        }

        public static void Publish<T>(T eventData)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list) || list.Count == 0)
                return;

            // ToArray로 복사 후 순회 (핸들러 내부에서 Unsubscribe해도 안전)
            var snapshot = list.ToArray();
            foreach (var handler in snapshot)
            {
                try
                {
                    ((Action<T>)handler)?.Invoke(eventData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameEventBus] 이벤트 처리 중 오류 ({typeof(T).Name}): {ex}");
                }
            }
        }

        /// <summary>씬 전환 또는 게임 종료 시 호출해 핸들러를 전부 제거한다.</summary>
        public static void Clear()
        {
            _handlers.Clear();
        }

        /// <summary>디버그용: 현재 등록된 핸들러 수를 반환한다.</summary>
        public static int GetHandlerCount<T>()
        {
            return _handlers.TryGetValue(typeof(T), out var list) ? list.Count : 0;
        }
    }
}
