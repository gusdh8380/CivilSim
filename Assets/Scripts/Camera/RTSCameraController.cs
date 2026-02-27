using UnityEngine;
using UnityEngine.InputSystem;

namespace CivilSim.Camera
{
    /// <summary>
    /// 심시티 스타일 RTS 카메라 컨트롤러 (New Input System).
    ///
    /// 조작:
    ///   Pan  : WASD / 방향키 / 화면 가장자리 마우스
    ///   Pan  : 중클릭 드래그
    ///   Zoom : 마우스 휠
    ///   Orbit: Alt + 좌클릭 드래그 (선택)
    /// </summary>
    public class RTSCameraController : MonoBehaviour
    {
        // ── 인스펙터 ────────────────────────────────────────

        [Header("Pan")]
        [SerializeField] private float _keyPanSpeed  = 25f;
        [SerializeField] private float _dragPanSpeed = 1f;
        [SerializeField] private bool  _edgePanning  = true;
        [SerializeField, Range(5f, 50f)] private float _edgeThreshold = 15f;

        [Header("Zoom")]
        [SerializeField] private float _zoomSpeed  = 8f;
        [SerializeField] private float _minZoom    = 10f;
        [SerializeField] private float _maxZoom    = 70f;
        [SerializeField] private float _zoomSmooth = 10f;

        [Header("Orbit")]
        [SerializeField] private bool  _allowOrbit    = true;
        [SerializeField] private float _orbitSpeed    = 180f;
        [SerializeField] private float _minPitch      = 20f;
        [SerializeField] private float _maxPitch      = 80f;

        [Header("Movement Smoothing")]
        [SerializeField] private float _moveSmoothTime = 0.12f;

        [Header("Bounds (World XZ)")]
        [SerializeField] private Vector2 _minBounds = new Vector2(0f,   0f);
        [SerializeField] private Vector2 _maxBounds = new Vector2(100f, 100f);

        // ── 내부 상태 ────────────────────────────────────────
        private UnityEngine.Camera _cam;

        // Pan
        private Vector3 _targetPos;
        private Vector3 _posVelocity;

        // Zoom (FOV or ortho size)
        private float _targetFOV;

        // Middle-drag
        private bool  _isMidDragging;
        private Vector3 _dragAnchor;        // 드래그 시작 시점 월드 좌표

        // Orbit
        private float _yaw;
        private float _pitch;
        private bool  _isOrbiting;

        // ── Unity ───────────────────────────────────────────

        private void Awake()
        {
            _cam = GetComponentInChildren<UnityEngine.Camera>();
            if (_cam == null) _cam = UnityEngine.Camera.main;

            _targetPos = transform.position;
            _targetFOV = _cam.orthographic ? _cam.orthographicSize : _cam.fieldOfView;

            // 현재 오일러 각도에서 yaw/pitch 초기화
            _yaw   = transform.eulerAngles.y;
            _pitch = transform.eulerAngles.x;
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

            // 카메라 정면 방향 기준으로 이동 (y 무시)
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right   = Vector3.ProjectOnPlane(transform.right,   Vector3.up).normalized;

            _targetPos += (forward * input.z + right * input.x) * (_keyPanSpeed * Time.deltaTime);
        }

        private void HandleEdgePan()
        {
            if (!_edgePanning) return;
            if (Mouse.current == null) return;

            // 에디터 Game 뷰에서 마우스가 윈도우 안에 있을 때만
#if UNITY_EDITOR
            if (!UnityEngine.Application.isFocused) return;
#endif

            Vector2 mp    = Mouse.current.position.ReadValue();
            float   sw    = Screen.width;
            float   sh    = Screen.height;
            float   speed = _keyPanSpeed * Time.deltaTime;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right   = Vector3.ProjectOnPlane(transform.right,   Vector3.up).normalized;

            if (mp.x < _edgeThreshold)           _targetPos -= right   * speed;
            else if (mp.x > sw - _edgeThreshold) _targetPos += right   * speed;
            if (mp.y < _edgeThreshold)            _targetPos -= forward * speed;
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

        // ── Zoom ─────────────────────────────────────────────

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            _targetFOV -= scroll * _zoomSpeed;
            _targetFOV  = Mathf.Clamp(_targetFOV, _minZoom, _maxZoom);
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
            // 위치
            transform.position = Vector3.SmoothDamp(
                transform.position, _targetPos, ref _posVelocity, _moveSmoothTime);

            // 회전
            if (_allowOrbit)
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // 줌
            if (_cam.orthographic)
                _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetFOV, Time.deltaTime * _zoomSmooth);
            else
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _targetFOV, Time.deltaTime * _zoomSmooth);
        }

        // ── 유틸 ─────────────────────────────────────────────

        /// 화면 좌표 → 지면(y=0) 월드 좌표
        private Vector3 RaycastGround(Vector2 screenPos)
        {
            Ray ray = _cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
                return ray.GetPoint(dist);
            return _targetPos;
        }

        // ── 공개 API ─────────────────────────────────────────

        /// GridSystem의 그리드 크기에 맞게 이동 경계를 설정한다.
        public void SetBoundsFromGrid(Vector2 gridMin, Vector2 gridMax)
        {
            _minBounds = gridMin;
            _maxBounds = gridMax;
        }

        /// 특정 월드 좌표로 카메라 순간이동
        public void TeleportTo(Vector3 worldPos)
        {
            _targetPos         = new Vector3(worldPos.x, _targetPos.y, worldPos.z);
            transform.position = _targetPos;
        }
    }
}
