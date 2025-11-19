using System;
using System.Collections.Generic;
using Cinemachine;
using Racing_Game.Scripts.Match.Camera_System.Focused_Racer;
using Racing_Game.Scripts.Match.Race_Track;
using Racing_Game.Scripts.Match.Racers;
using Sirenix.Utilities;
using UnityEngine;
using Utilities.Extensions;

namespace Racing_Game.Scripts.Match.Camera_System.Bird_Eye_Camera {
    public class BirdEyeCamera : MatchVirtualCamera {
        #region Serialized Fields

        [SerializeField] private float zoomOutMultiplier = 1.5f;
        [SerializeField] private float zoomOutSpeedMultiplier = 2f;
        [SerializeField] private float cameraSpeed = 1.0f;
        [SerializeField] private float minRacerSpeedMultiplier = 0.8f;
        [SerializeField] private float maxRacerSpeedMultiplier = 2f;
        [SerializeField] private float maxTweenLength = 20f;
        [SerializeField] private float cameraZoomSpeed = 1.0f;
        [SerializeField] private float lookAhead = 10.0f;

        [SerializeField] private (float min, float max) zoomRange;

        [SerializeField] private float serverTargetSpeed = 0.0f;
        [SerializeField] private float actualTargetSpeed = 0.0f;
        [SerializeField] private float physicalTargetSpeed = 0.0f;
        [SerializeField] private float serverSpeedScale = 1.0f;
        [SerializeField] private float minServerSpeedScale = 1.0f;
        [SerializeField] private float maxServerSpeedScale = 1.0f;

        #endregion

        private float _previousTargetPhase = -1.0f;

        private List<IRacerInfoProvider> _racerControllerList;
        private RaceTrack _trackData;
        private Vector3 _avgHorsePosition;
        private RaceTrack.ObjectPose _heatPose;
        private float _cameraPhase;
        private float _cameraSpeed;
        private float _cameraZoom;
        private bool _raceStarted;
        private bool _cameraZoomSet = false;

        public CinemachineVirtualCamera BirdVirtualCamera => VirtualCamera;

        public void Initialize(List<IRacerInfoProvider> racerInfoProviders, RaceTrack trackData) {
            _racerControllerList = racerInfoProviders;
            _trackData = trackData;
        }

        public void StartFollow() {
            _raceStarted = true;
        }

        private void LateUpdate() {
            if (!_raceStarted) return;
            if (_racerControllerList.IsNullOrEmpty()) return;
            var deltaTime = Time.deltaTime;
            UpdateZoom(deltaTime);
            Follow(deltaTime);
        }

        private void UpdateZoom(float deltaTime) {
            var virtualCameraTransform = VirtualCamera.transform;
            var up = virtualCameraTransform.up;
            var right = virtualCameraTransform.right;
            if (FocusedRacerProvider.GetFocusedRacer() == null) return;
            var heatPose = FocusedRacerProvider.GetHeatPose(_trackData).position;
            var heatedRacer = FocusedRacerProvider.GetFocusedRacer();

            Vector2 max;
            var min = max = ToCameraSpace(heatPose);

            var avgRacerPosition = heatPose;
            var relevantRacerCount = 0.0f;
            foreach (var racerInfoProvider in _racerControllerList) {
                var position = racerInfoProvider.Position;
                if (Vector3.Distance(position, heatPose) > zoomRange.max) continue;
                relevantRacerCount++;
                avgRacerPosition = Vector3.Lerp(avgRacerPosition, position, 1.0f / relevantRacerCount);
                IncludePos(ToCameraSpace(position));
                IncludePos(ToCameraSpace(
                    _trackData.GetPose(racerInfoProvider.Racer.LaneNumber,
                            racerInfoProvider.RacerState.Phase +
                            lookAhead * 1.0f / Math.Max(float.Epsilon, _trackData.Length))
                        .position));
            }

            IncludePos(ToCameraSpace(avgRacerPosition + (-up - right) * zoomRange.min));
            IncludePos(ToCameraSpace(avgRacerPosition + (-up + right) * zoomRange.min));
            IncludePos(ToCameraSpace(avgRacerPosition + (up - right) * zoomRange.min));
            IncludePos(ToCameraSpace(avgRacerPosition + (up + right) * zoomRange.min));

            var fov = VirtualCamera.m_Lens.FieldOfView;
            var tan = Mathf.Tan(Mathf.Deg2Rad * fov * 0.5f);
            var aspect = (float)Screen.width / Mathf.Max(Screen.height, 1);
            var halfSize = (max - min) * 0.5f;
            var dx = halfSize.x / Mathf.Max(tan * aspect, float.Epsilon);
            var dy = halfSize.y / Mathf.Max(tan, float.Epsilon);
            var desiredMultiplier =
                heatedRacer.RacerState.Speed.Map(1, heatedRacer.RacerStats.TopSpeed, 1, zoomOutSpeedMultiplier);
            var desiredCameraZoom = Mathf.Max(dx, dy) * zoomOutMultiplier * desiredMultiplier;
            if (_cameraZoomSet == false) {
                _cameraZoom = desiredCameraZoom;
                _cameraZoomSet = true;
            }
            else {
                _cameraZoom = Mathf.Lerp(_cameraZoom, desiredCameraZoom,
                    1.0f - Mathf.Exp(-deltaTime * cameraZoomSpeed));
            }

            return;

            void IncludePos(Vector2 relPos) {
                min.x = Mathf.Min(min.x, relPos.x);
                min.y = Mathf.Min(min.y, relPos.y);
                max.x = Mathf.Max(max.x, relPos.x);
                max.y = Mathf.Max(max.y, relPos.y);
            }

            Vector2 ToCameraSpace(Vector3 pos) {
                return new Vector2(Vector3.Dot(pos, right), Vector3.Dot(pos, up));
            }
        }

        private void Follow(float deltaTime) {
            var heatedRacer = FocusedRacerProvider.GetFocusedRacer();
            if (heatedRacer == null) return;
            var phaseToDistance = _trackData.Length;
            var distanceToPhase = 1.0f / Math.Max(float.Epsilon, phaseToDistance);

            var desiredPhase = heatedRacer.RacerState.Phase;
            var positionDelta = (desiredPhase - _cameraPhase) * phaseToDistance;
            var distance = Mathf.Abs(positionDelta);
            var racerSpeed = heatedRacer.RacerState.Speed;

            {
                serverTargetSpeed = racerSpeed;
                actualTargetSpeed = (heatedRacer.RacerState.Phase - _previousTargetPhase) / deltaTime * phaseToDistance;
                physicalTargetSpeed =
                    (_trackData.GetPose(heatedRacer.RacerState.Phase).position -
                     _trackData.GetPose(_previousTargetPhase).position).magnitude / deltaTime;
                if (serverTargetSpeed > 0.1f && actualTargetSpeed > 0.1f && _previousTargetPhase > 0.0f) {
                    serverSpeedScale = actualTargetSpeed / serverTargetSpeed;
                    minServerSpeedScale = Mathf.Min(minServerSpeedScale, serverSpeedScale);
                    maxServerSpeedScale = Mathf.Max(maxServerSpeedScale, serverSpeedScale);
                }

                _previousTargetPhase = heatedRacer.RacerState.Phase;
            }

            if (distance < maxTweenLength) {
                var catchUpSpeed = (deltaTime > 0.0f)
                    ? Mathf.Min(racerSpeed * maxRacerSpeedMultiplier,
                        Mathf.Max(positionDelta / deltaTime, racerSpeed * minRacerSpeedMultiplier))
                    : _cameraSpeed;
                _cameraSpeed = Mathf.Lerp(_cameraSpeed, catchUpSpeed,
                    1.0f - Mathf.Exp(-deltaTime * cameraSpeed * distance));
                _cameraPhase += _cameraSpeed * deltaTime * distanceToPhase;
            }
            else {
                _cameraPhase = desiredPhase;
                _cameraSpeed = racerSpeed;
            }

            var targetPoint = _trackData.GetPose(_cameraPhase).position;
            var transform1 = VirtualCamera.transform;
            transform1.position = targetPoint - _cameraZoom * transform1.forward;
        }
    }
}