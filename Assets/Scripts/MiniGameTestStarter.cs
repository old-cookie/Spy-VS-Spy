using UnityEngine;

public class MiniGameTestStarter : MonoBehaviour
{
	/// <summary>
	/// Mini game prefab to spawn for testing. Drag a prefab that derives from <see cref="MiniGame"/>.
	/// </summary>
	[Header("Mini Game Prefab")]
	[SerializeField] private MiniGame prefabToSpawn;

	/// <summary>
	/// Start settings: whether to automatically start on scene load and whether to make the timer UI visible.
	/// </summary>
	[Header("Start Settings")]
	[SerializeField] private bool startOnSceneLoad = true;
	[SerializeField] private bool ensureDisplayVisible = true;

	/// <summary>
	/// The spawned mini game instance created at runtime.
	/// </summary>
	private MiniGame _spawnedMiniGame;

	/// <summary>
	/// The timer component found within the spawned mini game (or its children).
	/// </summary>
	private MiniGameTimer _spawnedTimer;

	/// <summary>
	/// Scene entry point. Optionally spawns and starts the mini game.
	/// </summary>
	private void Start()
	{
		if (!startOnSceneLoad)
		{
			return;
		}

		if (prefabToSpawn == null)
		{
			return;
		}

		// Instantiate prefab under this starter for neat hierarchy
		_spawnedMiniGame = Instantiate(prefabToSpawn, transform);
		if (_spawnedMiniGame == null)
		{
			return;
		}

		// Auto-wire timer from spawned object (component or children)
		_spawnedTimer = _spawnedMiniGame.GetComponentInChildren<MiniGameTimer>(true);

		// Ensure active before starting (in case prefab default is disabled)
		if (!_spawnedMiniGame.gameObject.activeSelf)
		{
			_spawnedMiniGame.gameObject.SetActive(true);
		}

		// Start the mini game without player context (Canvas-only flow)
		_spawnedMiniGame.StartGame(null);

		// Optional: ensure display visible if timer exists; game controls StartTimer
		if (_spawnedTimer != null)
		{
			if (ensureDisplayVisible)
			{
				_spawnedTimer.SetDisplayVisible(true);
			}
		}
		else
		{
		}
	}
}
