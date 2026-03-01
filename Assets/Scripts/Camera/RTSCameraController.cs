using UnityEngine;
using UnityEngine.InputSystem;

namespace CivilSim.CameraSystem
{
    /// <summary>
    /// 심시티 스타일 RTS 카메라 컨트롤러 (New Input System).
    ///
    /// 조작:
    ///   Pan  : WASD / 방향키 / 화면 가장자리 마우스
    ///   Pan  : 중클릭 드래그
    ///   Zoom : 마우스 휠 (카메라 높이 이동)
    ///   Orbit: Alt + 좌클릭 드래그
    ///
    /// CellSize = 10 기준 기본값:
    ///   StartHeight 80, MinHeight 20, MaxHeight 300
    /// </summary>
    public class RTSCameraController : MonoBehaviour
    {
        // ── 인스펙터 ────────────────────────────────────────

        [Header("Pan")]
        [Tooltip("수평 이동 기본 속도 (높이에 따라 자동 스케일됨)")]
        [SerializeField] private float _keyPanSpeed   = 0.8f;   // height 배수 per second
        [SerializeField] private bool  _edgePanning   = true;
        [SerializeField, Range(5f, 50f)] private float _edgeThreshold = 15f;

        [Header("Zoom (Height)")]
        [Tooltip("마우스 휠 한 틱당 이동 높이 (units)")]
        [SerializeField] private float _zoomSpeed    = 25f;
        [Tooltip("카메라 최저 높이 (가장 가까운 줌)")]
        [SerializeField] private float _minHeight    = 20f;
        [Tooltip("카메라 최고 높이 (가장 먼 줌)")]
        [SerializeField] private float _maxHeight    = 300f;
        [SerializeField] private float _zoomSmooth   = 10f;
        [Tooltip("씬 시작 시 카메라 높이. 0이면 현재 트랜스폼 Y 사용.")]
        [SerializeField] private float _startHeight  = 80f;

        [Header("Orbit")]
        [SerializeField] private bool  _allowOrbit = true;
        [SerializeField] private float _orbitSpeed = 180f;
        [SerializeField] private float _minPitch   = 20f;
        [SerializeField] private float _maxPitch   = 80f;

        [Header("Movement Smoothing")]
        [SerializeField] private float _moveSmoothTime = 0.12f;

        [Header("Bounds (World XZ)")]
        [SerializeField] private Vector2 _minBounds = new Vector2(0f,    0f);
        [SerializeField] private Vector2 _maxBounds = new Vector2(1000f, 1000f);

        // ── 내부 상태 ────────────────────────────────────────
        private UnityEngine.Camera _cam;

        // Pan + Height (targetPos.y = camera height)
        private Vector3 _targetPos;
        private Vector3 _posVelocity;
        private float   _targetHeight;
        private float   _heightVelocity;

        // Middle-drag
        private bool    _isMidDragging;
        private Vector3 _dragAnchor;

        // Orbit
        private float _yaw;
        private float _pitch;
        private bool  _isOrbiting;

        // ── Unity ───────────────────────────────────────────

        private void Awake()
        {
            _cam = GetComponentInChildren<UnityEngine.Camera>();
            if (_cam == null) _cam = UnityEngine.Camera.main;

            // XZ 목표 위치 초기화
            _targetPos = transform.position;

            // 시작 높이 적용 (_startHeight > 0 이면 강제 설정)
            if (_startHeight > 0.1f)
                _targetPos.y = _startHeight;

            _targetHeight = _targetPos.y;

            // 현재 오일러 각도에서 yaw/pitch 초기화
            _yaw   = transform.eulerAngles.y;
            _pitch = transform.eulerAngles.x;

            // pitch 미설정 시 기본 45° 부감각
            if (Mathf.Approximately(_pitch, 0f))
                _pitch = 45f;
        }

        private void Update()
        {
            HandleKeyboardPan();
            HandleEdgePan();
            HandleMiddleDrag();
            HandleZoom();

            if (_allowOrbit)
                HandleOrbit();

            ClampTargetPos();
            ApplySmoothing();
        }

        // ── Pan ─────────────────────────────────────────────

        private void HandleKeyboardPan()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            Vector3 input = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    input.z += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  input.z -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;

            if (input == Vector3.zero) return;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right   = Vector3.ProjectOnPlane(transform.right,   Vector3.up).normalized;

            // 높이에 비례한 팬 속도 (높이 올라갈수록 빠르게)
            float speed = _keyPanSpeed * _targetHeight * Time.deltaTime;
            _targetPos += (forward * input.z + right * input.x) * speed;
        }

        private void HandleEdgePan()
        {
            if (!_edgePanning) return;
            if (Mouse.current == null) return;

#if UNITY_EDITOR
            if (!UnityEngine.Application.isFocused) return;
#endif

            Vector2 mp    = Mouse.current.position.ReadValue();
            float   sw    = Screen.width;
            float   sh    = Screen.height;
            float   speed = _keyPanSpeed * _targetHeight * Time.deltaTime;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right   = Vector3.ProjectOnPlane(transform.right,   Vector3.up).normalized;

            if (mp.x < _edgeThreshold)            _targetPos -= right   * speed;
            else if (mp.x > sw - _edgeThreshold)  _targetPos += right   * speed;
            if (mp.y < _edgeThreshold)             _targetPos -= forward * speed;
            else if (mp.y > sh - _edgeThreshold)  _targetPos += forward * speed;
        }

        private void HandleMiddleDrag()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.middleButton.wasPressedThisFrame)
            {
                _isMidDragging = true;
                _dragAnchor    = RaycastGround(mouse.position.ReadValue());
            }

            if (mouse.middleButton.wasReleasedThisFrame)
                _isMidDragging = false;

            if (!_isMidDragging) return;

            Vector3 current = RaycastGround(mouse.position.ReadValue());
            Vector3 delta   = _dragAnchor - current;
            _targetPos     += new Vector3(delta.x, 0f, delta.z);
        }

        // ── Zoom (Height) ─────────────────────────────────────

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            // 휠 위 = 줌인 (높이 낮아짐), 휠 아래 = 줌아웃 (높이 높아짐)
            _targetHeight -= scroll * _zoomSpeed;
            _targetHeight  = Mathf.Clamp(_targetHeight, _minHeight, _maxHeight);
        }

        // ── Orbit ────────────────────────────────────────────

        private void HandleOrbit()
        {
            var mouse    = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null) return;

            bool altHeld = keyboard.altKey.isPressed;
            bool lmb     = mouse.leftButton.isPressed;

            _isOrbiting = altHeld && lmb;
            if (!_isOrbiting) return;

            Vector2 delta = mouse.delta.ReadValue();
            _yaw   += delta.x * _orbitSpeed * Time.deltaTime;
            _pitch -= delta.y * _orbitSpeed * Time.deltaTime;
            _pitch  = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
        }

        // ── Apply ─────────────────────────────────────────────

        private void ClampTargetPos()
        {
            _targetPos.x = Mathf.Clamp(_targetPos.x, _minBounds.x, _maxBounds.x);
            _targetPos.z = Mathf.Clamp(_targetPos.z, _minBounds.y, _maxBounds.y);
        }

        private void ApplySmoothing()
        {
            // 높이 스무딩
            float smoothedHeight = Mathf.SmoothDamp(
                transform.position.y, _targetHeight, ref _heightVelocity, _moveSmoothTime);
            _targetPos.y = smoothedHeight;

            // 위치 스무딩 (XZ)
            transform.position = Vector3.SmoothDamp(
                transform.position, _targetPos, ref _posVelocity, _moveSmoothTime);

            // 회전
            if (_allowOrbit)
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // ── 유틸 ─────────────────────────────────────────────

        private Vector3 RaycastGround(Vector2 screenPos)
        {
            Ray ray   = _cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
                return ray.GetPoint(dist);
            return _targetPos;
        }

        // ── 공개 API ─────────────────────────────────────────

        public void SetBoundsFromGrid(Vector2 gridMin, Vector2 gridMax)
        {
            _minBounds = gridMin;
            _maxBounds = gridMax;
        }

        public void TeleportTo(Vector3 worldPos)
        {
            _targetPos         = new Vector3(worldPos.x, _targetPos.y, worldPos.z);
            transform.position = _targetPos;
        }
    }
}
