using System;
using System.Collections.Generic;
using System.Linq;
using Racing_Game.Scripts.Match.Environment.Data_Types;
using Racing_Game.Scripts.Match.Race_Track.Obstacles.Data_Structures;
using Racing_Game.Scripts.UI.In_Game.Minimap;
using Sirenix.Utilities;
using UnityEngine;
using Utilities;
using Utilities.Bezier;

namespace Racing_Game.Scripts.Match.Race_Track {
    [Serializable]
    public class RaceTrack : ITrackShapeProvider, ITrackInfoProvider {
        private const float TrackCenterOffsetFraction = 0f;

        [SerializeField] private BezierSpline<Vector2> trackShape;
        [SerializeField] private List<List<RaceTrackObstacleData>> obstacleDataList;
        [SerializeField] private EnvironmentType environmentType;
        [SerializeField] private int numLanes;
        [SerializeField] private Transform trackTransform;

        [SerializeField] private float laneWidth;
        [SerializeField] private float curvatureStepDelta;

        private SplinePoseCalculator _splinePoseCalculator;

        private static readonly int SIMULATION_STEPS = 100;

        public RaceTrack(BezierSpline<Vector2> trackShape, Transform trackTransform, int numLanes,
            RaceTrackSettings trackSettings, List<List<RaceTrackObstacleData>> obstacleDataList,
            EnvironmentType environmentType) {
            this.numLanes = numLanes;
            this.trackShape = trackShape;
            this.trackTransform = trackTransform;

            laneWidth = trackSettings.LaneWidth;
            curvatureStepDelta = trackSettings.CurvatureStepDelta;

            this.obstacleDataList = obstacleDataList == null
                ? new List<List<RaceTrackObstacleData>>()
                : obstacleDataList.Select(x => x ?? new List<RaceTrackObstacleData>()).ToList();
            this.environmentType = environmentType;

            while (this.obstacleDataList.Count < numLanes) {
                this.obstacleDataList.Add(new List<RaceTrackObstacleData>());
            }

            _splinePoseCalculator = new SplinePoseCalculator(this.trackShape);
        }

        public void CleanData() {
            _splinePoseCalculator = new SplinePoseCalculator(trackShape);
        }

        #region Public Properties & Methods

        public Vector3 Forward => Transform.forward;
        public Vector3 Right => Transform.right;
        public float LaneWidth => laneWidth;
        public int NumLanes => numLanes;
        public event Action OnDirty;
        public event Action OnDestroyed;

        public BezierSpline<Vector2> TrackShape => CopyTrackShape();

        public float CurvatureStepDelta => curvatureStepDelta;

        public List<RaceTrackObstacleData> ObstacleDataList(int laneId) {
            var index = laneId - 1;
            return index < obstacleDataList.Count && obstacleDataList[index] != null
                ? obstacleDataList[index]
                : new List<RaceTrackObstacleData>();
        }

        public EnvironmentType EnvironmentType => environmentType;

        public float Length => _splinePoseCalculator.TrackLength;

        public Transform Transform => trackTransform;

        private float TrackCenterOffset => -(LaneWidth * NumLanes * TrackCenterOffsetFraction);

        public static float Step => 1.0f / SIMULATION_STEPS;

        /// <summary>
        /// Calculates track curvature at a given phase for physics-based speed modulation
        /// </summary>
        /// <param name="phase">Normalized position along track [0-1]</param>
        /// <returns>Curvature value where 0 = straight, higher = sharper turn</returns>
        public float Curvature(float phase) {
            var a = GetPose(phase).forward;
            var b = GetPose(phase + CurvatureStepDelta / Mathf.Max(Length, float.Epsilon)).forward;
            return 0.5f - Vector3.Dot(a, b) * 0.5f;
        }

        public float CurvatureAngle(float phase) {
            var curvature = Curvature(phase);
            var cosine = Mathf.Clamp((0.5f - curvature) * 2.0f, -1.0f, 1.0f);
            return Mathf.Acos(cosine) / CurvatureStepDelta * Mathf.Rad2Deg;
        }

        public ObjectPose GetPose(int laneId, float phase) {
            return trackShape == null ? new ObjectPose() : EvaluatePose(laneId, phase);
        }

        public ObjectPose GetPose(float phase) {
            return trackShape == null ? new ObjectPose() : EvaluatePose((NumLanes - 1) / 2.0f, phase);
        }

        public ObjectPose GetOffsetPose(float phase, float centerOffset) {
            if (trackShape == null || Transform == null) return new ObjectPose();

            return _splinePoseCalculator.CalculatePose(phase, centerOffset + TrackCenterOffset,
                Transform.localToWorldMatrix);
        }

        public ObjectPose EvaluatePose(float laneId, float time) {
            if (trackShape == null || Transform == null) return new ObjectPose();
            return _splinePoseCalculator.CalculatePose(time,
                (laneId + 0.5f - NumLanes * 0.5f) * LaneWidth + TrackCenterOffset,
                Transform.localToWorldMatrix);
        }

        public bool ObstacleExists(int laneId, float phase) {
            if (laneId < 0 || laneId >= NumLanes) return false;
            var dataList = ObstacleDataList(laneId);
            if (dataList.IsNullOrEmpty()) return false;
            return dataList.Exists(x => x.StartPosition < phase && x.StartPosition + x.Length > phase);
        }

        public bool ObstacleExists(float phase) {
            for (int i = 1; i <= numLanes; i++)
                if (ObstacleExists(i, phase))
                    return true;
            return false;
        }

        #endregion

        #region Helpher Methods

        private BezierSpline<Vector2> CopyTrackShape() {
            var shape = new BezierSpline<Vector2>();
            trackShape.ForEach(x => {
                var node = new BezierNode<Vector2> {
                    Position = x.Position,
                    LinkHandles = x.LinkHandles,
                    NextHandle = x.NextHandle,
                    PrevHandle = x.PrevHandle
                };
                shape.Add(node);
            });
            return shape;
        }

        #endregion

        public struct ObjectPose {
            public Vector3 position;
            public Vector3 forward;
        }
    }

    public interface ITrackInfoProvider {
        public int NumLanes { get; }
        public float Length { get; }
        public List<RaceTrackObstacleData> ObstacleDataList(int laneId);
    }
}