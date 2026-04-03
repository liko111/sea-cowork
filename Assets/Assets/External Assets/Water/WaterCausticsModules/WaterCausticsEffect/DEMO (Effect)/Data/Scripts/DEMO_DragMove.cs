// WaterCausticsModules
// Copyright (c) 2021 Masataka Hakozaki

using UnityEngine;

namespace MH.WaterCausticsModules {

    [DisallowMultipleComponent]
    [AddComponentMenu ("")]
    public class DEMO_DragMove : MonoBehaviour {
#if UNITY_EDITOR
        readonly private float DAMPING = 0.1f;
        private Vector3 _tarPos, _veloPos, _offset;
        private Plane _plane;
        private bool _isTouch;
        private Collider _col;
        private Camera _cam;
        private Transform _tra;

        private void OnEnable () {
            _tra = transform;
            _tarPos = transform.position;
            _veloPos = Vector3.zero;
            _col = GetComponent<Collider> ();
            _cam = Camera.main;
            if (_col == null || _cam == null) enabled = false;
        }

        private void Update () {
            if (DEMO_InputWrapper.GetMouseButtonDown (1)) {
                var mousePos = DEMO_InputWrapper.mousePosition;
                Ray ray = _cam.ScreenPointToRay (mousePos);
                if (_col.Raycast (ray, out var hitInfo, 100f)) {
                    _isTouch = true;
                    Vector3 planeNorm = getAxis (_cam.transform.forward);
                    _plane = new Plane (planeNorm, hitInfo.point);
                    _offset = _tra.position - hitInfo.point;
                    _tarPos = _tra.position;
                    _veloPos = Vector3.zero;
                }
            }
            if (_isTouch) {
                if (DEMO_InputWrapper.GetMouseButton (1) && getMousePtOnPlane (out var mousePt)) {
                    _tarPos = mousePt + _offset;
                }
                if (DEMO_InputWrapper.GetMouseButtonUp (1)) {
                    _isTouch = false;
                }
            }
            if (_tra.position != _tarPos) {
                _tra.position = smoothDampSafe (_tra.position, _tarPos, ref _veloPos, DAMPING, Time.unscaledDeltaTime);
            }
        }

        private bool getMousePtOnPlane (out Vector3 mousePt) {
            Ray ray = _cam.ScreenPointToRay (DEMO_InputWrapper.mousePosition);
            bool result = _plane.Raycast (ray, out float enter);
            mousePt = result ? ray.GetPoint (enter) : Vector3.zero;
            return result;
        }

        private static Vector3 smoothDampSafe (Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float deltaTime) {
            if (deltaTime == 0f) {
                return current;
            } else if (smoothTime <= float.Epsilon) {
                currentVelocity = Vector3.zero;
                return target;
            }
            return Vector3.SmoothDamp (current, target, ref currentVelocity, smoothTime, Mathf.Infinity, deltaTime);
        }

        private Vector3 getAxis (Vector3 v) {
            float x = Mathf.Abs (v.x);
            float y = Mathf.Abs (v.y);
            float z = Mathf.Abs (v.z);
            if (z >= x && z >= y) {
                return Vector3.forward;
            } else if (x >= y) {
                return Vector3.right;
            } else {
                return Vector3.up;
            }
        }
#endif
    }
}
