using UnityEngine;
using System;

namespace CapybaraDuel.Core
{
    /// <summary>
    /// 游戏管理器 - 管理游戏状态机和协调各子系统
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            Idle = 0,
            Connecting = 1,
            Waiting = 2,
            Running = 3,
            Paused = 4,
            Settlement = 5,
            Ended = 6
        }

        [Header("Current State")]
        [SerializeField] private GameState currentState = GameState.Idle;

        public GameState CurrentState => currentState;

        // 事件
        public event Action<GameState, GameState> OnStateChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // TODO: 初始化各子系统
        }

        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            var oldState = currentState;
            currentState = newState;

            Debug.Log($"[GameManager] State changed: {oldState} -> {newState}");
            OnStateChanged?.Invoke(oldState, newState);
        }

        public void StartGame()
        {
            ChangeState(GameState.Running);
        }

        public void PauseGame()
        {
            if (currentState == GameState.Running)
            {
                ChangeState(GameState.Paused);
            }
        }

        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
            {
                ChangeState(GameState.Running);
            }
        }

        public void EndGame(string winner, string reason)
        {
            Debug.Log($"[GameManager] Game ended: {winner} wins - {reason}");
            ChangeState(GameState.Settlement);
        }

        public void ResetGame()
        {
            // TODO: 重置所有游戏状态
            ChangeState(GameState.Idle);
        }
    }
}
