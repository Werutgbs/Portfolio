using System;
using System.Collections.Generic;
using Racing_Game.Scripts.Alpaca_Animations;
using Racing_Game.Scripts.Alpaca_Nft_Building;
using Racing_Game.Scripts.Match.Camera_System.Racer_Camera;
using Racing_Game.Scripts.Match.Game_Loop;
using Racing_Game.Scripts.Match.Race_Track;
using Racing_Game.Scripts.Match.Racers.Data_Types;
using Racing_Game.Scripts.Match.Racers.Racer_Brains;
using Racing_Game.Scripts.Match.Racers.Skill_Effects;
using Racing_Game.Scripts.Match.Services.Match_Snapshot;
using Racing_Game.Scripts.Skills.VFX;
using Racing_Game.Scripts.UI.In_Game.DataTypes;
using Racing_Game.Scripts.UI.In_Game.Racer;
using Sirenix.OdinInspector;
using UnityEngine;
using World_Interaction.Selection;

namespace Racing_Game.Scripts.Match.Racers {
    public interface IRacerInfoProvider {
        public Racer Racer { get; }
        public RacerState RacerState { get; }
        public RacerStats RacerStats { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }

        public bool HasFinishedRace { get; }
        public int Placement { get; }
        public RacerViewCameraProvider CameraProvider { get; }
        public RacerUIController UIController { get; }
        public Transform HeadTransform { get; }
        public Transform RacerTransform { get; }
        public ITrackInfoProvider TrackInfoProvider { get; }

        public event Action<RacerState> OnStateUpdated;
        public event Action<int, int> OnLapUpdated;
        public event Action<bool> OnRaceFinishUpdated;

        public IRacerSkillUpdateProvider SkillUpdateProvider { get; }
        public event Action<IRacerInfoProvider> OnRacerClicked;
        public bool IsSelected { get; }

        public (long, List<RacerState>) GenerateFullRacerStates();
    }

    public class RacerController : SerializedMonoBehaviour, IRacerInfoProvider {
        #region Serialized Fields

        [SerializeField, BoxGroup("Controllers")]
        private RacerViewCameraProvider racerViewCamera;

        [SerializeField, BoxGroup("Controllers")]
        private NftAlpacaBuilder alpacaBuilder;

        [SerializeField, BoxGroup("Controllers")]
        private RacerAnimationController racerAnimationController;

        [SerializeField, BoxGroup("Controllers")]
        private RacerUIController uiController;

        [SerializeField, BoxGroup("Controllers")]
        private RacerSkillStatusController skillStatusController;

        [SerializeField, BoxGroup("Controllers")]
        private RacerSelector selector;

        [SerializeField] private Transform headTransform;
        [SerializeField] private SkillEffectsController skillEffectsController;

        #endregion

        #region Private Fields

        private RaceTrack _trackData;
        private IRacerBrain _racerBrain;
        private RacerState _racerState;
        private Racer _racer;
        private RacerStats _racerStats;
        private int _placement;

        #endregion

        #region IRacerInfoProvider

        public Racer Racer => _racer;
        public RacerStats RacerStats => _racerStats;
        public RacerState RacerState => _racerState;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;
        public bool HasFinishedRace { get; private set; }
        public int Placement => _placement;
        public Transform RacerTransform => transform;
        public ITrackInfoProvider TrackInfoProvider => _trackData;
        public event Action<RacerState> OnStateUpdated;

        public RacerUIController UIController => uiController;
        public Transform HeadTransform => headTransform;

        public RacerViewCameraProvider CameraProvider => racerViewCamera;
        public event Action<int, int> OnLapUpdated;
        public event Action<bool> OnRaceFinishUpdated;

        public IRacerSkillUpdateProvider SkillUpdateProvider => skillStatusController;
        public event Action<IRacerInfoProvider> OnRacerClicked;
        public bool IsSelected => selector.IsSelected;

        public (long, List<RacerState>) GenerateFullRacerStates() {
            if (_racerBrain is RacerBrain racerBrain) {
                return racerBrain.GetRacerStates();
            }

            return new ValueTuple<long, List<RacerState>>(0, new List<RacerState>());
        }

        #endregion

        public void Initialize(RaceTrack trackData, RacerData racerData,
            ISkillVisualEffectManager skillVisualEffectManager) {
            _racer = racerData.Racer;
            _racerStats = racerData.Stats;
            _trackData = trackData;
            _racerState = new RacerState();
            racerAnimationController.Initialize(racerData, this);
            uiController.Initialize(racerData);

            alpacaBuilder.Build(racerData.Racer.Alpaca.Parts);
            skillEffectsController.Initialize(this, skillVisualEffectManager);

            selector.OnClicked += RacerClickHandler;
            selector.Setup(racerData.Racer.LaneNumber);

            UpdateRacerPosition(0f);
        }

        public void ActivateRacerBrain(IRacerBrain racerBrain, RaceSnapshotManager racingDataProvider) {
            _racerBrain = racerBrain;
            _racerBrain.Initialize(this, racingDataProvider);
        }

        public void SetState(MatchStateType matchStateType) {
            racerAnimationController.UpdateRaceState(matchStateType);
        }

        public void StartRace() {
            _racerBrain.Activate();
            UIController.Show();
            selector.CanBeInteracted = true;
        }

        public void FinishRace() {
            UIController.Hide();
            selector.CanBeInteracted = false;
        }

        public void EndGame() {
            gameObject.SetActive(false);
        }

        public void SetRacerFinished(bool value) {
            HasFinishedRace = value;
            OnRaceFinishUpdated?.Invoke(value);
        }

        public void SelectRacer() {
            selector.Select();
        }

        public void DeselectRacer() {
            selector.Deselect();
        }

        public void UpdateRacer(long currentTime) {
            var currentLapCount = _racerState.Lap;

            _racerState = _racerBrain.GetCurrentRacerState(currentTime);
            skillStatusController.UpdateSkillState(_racerState);

            if (currentLapCount != _racerState.Lap) {
                OnLapUpdated?.Invoke(_racer.LaneNumber, _racerState.Lap);
            }

            OnStateUpdated?.Invoke(_racerState);

            UpdateRacerPosition(_racerState.Phase);
        }

        public void UpdateRacer() {
            var currentLapCount = _racerState.Lap;

            _racerState = _racerBrain.GetCurrentRacerState();
            skillStatusController.UpdateSkillState(_racerState);

            if (currentLapCount != _racerState.Lap) {
                OnLapUpdated?.Invoke(_racer.LaneNumber, _racerState.Lap);
            }

            OnStateUpdated?.Invoke(_racerState);

            UpdateRacerPosition(_racerState.Phase);
        }

        public void UpdateRacerPlacement(int placement) {
            _placement = placement;
        }

        private void UpdateRacerPosition(float phase) {
            var pos = _trackData.GetPose(_racer.LaneNumber - 1, phase);
            transform.position = pos.position;
            transform.LookAt(pos.position + pos.forward, transform.up);
        }

        private void RacerClickHandler() {
            OnRacerClicked?.Invoke(this);
        }
    }
}