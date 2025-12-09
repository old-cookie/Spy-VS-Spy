using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

/// <summary>
/// Controls player movement, jumping, facing direction, item interaction, and chest picking.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ItemEffectHandler))]
[RequireComponent(typeof(TeamMember))]
public class PlayerController : NetworkBehaviour
{
    /// <summary>
    /// Base movement speed of the player.
    /// </summary>
    [SerializeField]
    private float playerSpeed = 5f;

    /// <summary>
    /// Force applied when the player jumps.
    /// </summary>
    [Header("Jump Settings")]
    [SerializeField]
    private float jumpForce = 6f;

    /// <summary>
    /// The renderers to apply team materials to (supports multiple body parts).
    /// </summary>
    [Header("Team Settings")]
    [SerializeField]
    private SkinnedMeshRenderer[] playerRenderers;

    /// <summary>
    /// Material for blue team players.
    /// </summary>
    [SerializeField]
    private Material blueTeamMaterial;

    /// <summary>
    /// Material for red team players.
    /// </summary>
    [SerializeField]
    private Material redTeamMaterial;

    /// <summary>
    /// Transform position used for ground detection.
    [Header("Ground Check Settings")]
    public Transform groundCheckPos;

    /// <summary>
    /// Layer mask for ground detection.
    /// </summary>
    public LayerMask groundMask;

    /// <summary>
    /// Radius for ground check sphere.
    /// </summary>
    public float groundDistance = 0.4f;

    /// <summary>
    /// Whether the player is currently touching ground.
    /// </summary>
    public bool isGround = false;

    /// <summary>
    /// Maximum height of steps the player can climb.
    /// </summary>
    [Header("Step Settings")]
    [SerializeField, Range(0.05f, 1f)]
    private float stepHeight = 0.5f;

    /// <summary>
    /// Distance to check for steps in front of the player.
    /// </summary>
    [SerializeField, Range(0.05f, 1f)]
    private float stepCheckDistance = 0.3f;

    /// <summary>
    /// Speed at which the player smoothly climbs steps.
    /// </summary>
    [SerializeField, Range(0.5f, 10f)]
    private float stepSmoothSpeed = 10f;

    /// <summary>
    /// Speed at which the player rotates to face a new direction.
    /// </summary>
    [Header("Facing Settings")]
    [SerializeField, Range(0.1f, 20f)]
    private float rotationSmoothSpeed = 10f;

    /// <summary>
    /// Duration in seconds that the player is locked during the pick animation.
    /// </summary>
    [Header("Pick Settings")]
    [SerializeField]
    private float pickLockDuration = 1.3f;

    /// <summary>
    /// Fallback radius to detect chests when trigger events are missed.
    /// </summary>
    [SerializeField, Min(0.1f)]
    private float chestPickupRadius = 1.25f;

    /// <summary>
    /// Fallback radius to detect flags when trigger events are missed.
    /// </summary>
    [SerializeField, Min(0.1f)]
    private float flagPickupRadius = 1.25f;

    /// <summary>
    /// Y position threshold below which the player is reset to the nearest floor.
    /// </summary>
    [Header("Fall Reset Settings")]
    [SerializeField]
    private float fallResetThreshold = -20f;

    /// <summary>
    /// Height offset above the floor when resetting player position after falling.
    /// </summary>
    [SerializeField]
    private float floorSnapHeightOffset = 2f;

    /// <summary>
    /// Reference to the player's Animator component.
    /// </summary>
    public Animator animator;

    private Rigidbody playerRigidbody;

    /// <summary>
    /// Whether the player is currently landed (grounded).
    /// </summary>
    [SerializeField]
    private bool isLand = true;

    private Quaternion rightFacingRotation;
    private Quaternion leftFacingRotation;
    private Quaternion frontFacingRotation;
    private Quaternion targetFacingRotation;
    private bool isFacingRight = true;
    private float pickLockTimer;
    private bool shouldRestoreFacingAfterPick;
    private bool wasFacingRightBeforePick;
    private bool canPickChest;
    private ChestController currentChest;
    private Item heldItem;
    private string heldItemType;
    private ItemEffectHandler itemEffectHandler;
    private bool canPickFlag;
    private FlagTrigger currentFlag;
    private TeamMember teamMember;
    private bool isPlayingMiniGame;
    private InputSystem_Actions inputActions;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction pickAction;
    private InputAction useAction;

    private void Awake()
    {
        InitializeInputActions();
        CacheAnimatorReference();
        CacheRigidbodyReference();
        CacheItemEffectHandler();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CacheFacingRotations();
        IgnorePlayerCollisions();
        CacheTeamMember();
        SubscribeToTeamChanges();
        ApplyTeamMaterial(teamMember != null ? teamMember.CurrentTeam : Team.None);

        if (IsLocalPlayer)
        {
            EnablePlayerInput();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        DisablePlayerInput();
        UnsubscribeFromTeamChanges();
    }

    private void OnDisable()
    {
        DisablePlayerInput();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        DisposeInputActions();
    }

    /// <summary>
    /// Ignores collisions between this player and all other players in the scene.
    /// </summary>
    private void IgnorePlayerCollisions()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        var myCollider = GetComponent<Collider>();
        
        if (myCollider == null)
        {
            return;
        }

        foreach (var player in players)
        {
            if (player == gameObject)
            {
                continue;
            }

            var otherCollider = player.GetComponent<Collider>();
            if (otherCollider != null)
            {
                Physics.IgnoreCollision(myCollider, otherCollider, true);
            }
        }
    }

    private void Update()
    {
        if (!IsLocalPlayer)
        {
            return;
        }

        // Handle ESC key for mini game exit
        HandleMiniGameEscapeInput();

        // Block all input while playing mini game
        if (isPlayingMiniGame)
        {
            return;
        }

        CheckGround();
        UpdateGroundStatus();
        UpdatePickLockTimer();
        CheckFallReset();

        var moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        var pickLocked = IsPickLocked();
        var horizontal = pickLocked ? 0f : moveInput.x;

        UpdateFacingDirection(moveInput.x);

        var hasMovement = !Mathf.Approximately(horizontal, 0f);

        SetRunningAnimation(hasMovement);

        if (hasMovement)
        {
            TryStepClimb(Mathf.Sign(horizontal));
            var speedMultiplier = itemEffectHandler != null ? itemEffectHandler.CurrentSpeedMultiplier : 1f;
            var moveSpeed = playerSpeed * speedMultiplier;
            var delta = moveSpeed * Time.deltaTime * new Vector3(horizontal, 0f, 0f);
            transform.Translate(delta, Space.World);
        }

        SmoothFacingRotation();
    }

    /// <summary>
    /// Caches the Animator component reference.
    /// </summary>
    private void CacheAnimatorReference()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    /// <summary>
    /// Caches the Rigidbody component reference.
    /// </summary>
    private void CacheRigidbodyReference()
    {
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody>();
        }
    }

    /// <summary>
    /// Caches the ItemEffectHandler component reference.
    /// </summary>
    private void CacheItemEffectHandler()
    {
        if (itemEffectHandler == null)
        {
            itemEffectHandler = GetComponent<ItemEffectHandler>();
        }
    }

    /// <summary>
    /// Caches the TeamMember component reference.
    /// </summary>
    private void CacheTeamMember()
    {
        if (teamMember == null)
        {
            teamMember = GetComponent<TeamMember>();
        }
    }

    /// <summary>
    /// Subscribes to team change events from TeamMember.
    /// </summary>
    private void SubscribeToTeamChanges()
    {
        if (teamMember == null)
        {
            return;
        }

        // Use reflection to access the private NetworkVariable and subscribe
        var teamField = typeof(TeamMember).GetField("team", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (teamField != null)
        {
            var networkVar = teamField.GetValue(teamMember) as NetworkVariable<Team>;
            if (networkVar != null)
            {
                networkVar.OnValueChanged += OnTeamChanged;
            }
        }
    }

    /// <summary>
    /// Unsubscribes from team change events.
    /// </summary>
    private void UnsubscribeFromTeamChanges()
    {
        if (teamMember == null)
        {
            return;
        }

        var teamField = typeof(TeamMember).GetField("team", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (teamField != null)
        {
            var networkVar = teamField.GetValue(teamMember) as NetworkVariable<Team>;
            if (networkVar != null)
            {
                networkVar.OnValueChanged -= OnTeamChanged;
            }
        }
    }

    /// <summary>
    /// Called when the player's team changes.
    /// </summary>
    /// <param name="oldTeam">The previous team.</param>
    /// <param name="newTeam">The new team.</param>
    private void OnTeamChanged(Team oldTeam, Team newTeam)
    {
        ApplyTeamMaterial(newTeam);
    }

    /// <summary>
    /// Applies the appropriate material based on the player's team.
    /// </summary>
    /// <param name="team">The team to apply material for.</param>
    private void ApplyTeamMaterial(Team team)
    {
        if (playerRenderers == null || playerRenderers.Length == 0)
        {
            return;
        }

        Material materialToApply = null;
        switch (team)
        {
            case Team.Blue:
                materialToApply = blueTeamMaterial;
                break;
            case Team.Red:
                materialToApply = redTeamMaterial;
                break;
            default:
                return;
        }

        if (materialToApply == null)
        {
            return;
        }

        foreach (var renderer in playerRenderers)
        {
            if (renderer != null)
            {
                renderer.material = materialToApply;
            }
        }
    }

    /// <summary>
    /// Caches the rotation quaternions for different facing directions.
    /// </summary>
    private void CacheFacingRotations()
    {
        rightFacingRotation = transform.localRotation;
        leftFacingRotation = transform.localRotation * Quaternion.Euler(0f, 180f, 0f);
        frontFacingRotation = transform.localRotation * Quaternion.Euler(0f, -90f, 0f);
        targetFacingRotation = transform.localRotation;
    }

    /// <summary>
    /// Updates the player's facing direction based on horizontal input.
    /// </summary>
    /// <param name="horizontal">Horizontal input value.</param>
    private void UpdateFacingDirection(float horizontal)
    {
        if (IsPickLocked())
        {
            return;
        }

        if (horizontal > 0f && !isFacingRight)
        {
            FaceRight();
        }
        else if (horizontal < 0f && isFacingRight)
        {
            FaceLeft();
        }
    }

    /// <summary>
    /// Sets the target rotation to face right.
    /// </summary>
    private void FaceRight()
    {
        targetFacingRotation = rightFacingRotation;
        isFacingRight = true;
    }

    /// <summary>
    /// Sets the target rotation to face left.
    /// </summary>
    private void FaceLeft()
    {
        targetFacingRotation = leftFacingRotation;
        isFacingRight = false;
    }

    /// <summary>
    /// Sets the running animation state.
    /// </summary>
    /// <param name="isRunning">Whether the player is running.</param>
    private void SetRunningAnimation(bool isRunning)
    {
        CacheAnimatorReference();

        if (animator == null)
        {
            return;
        }

        animator.SetBool("isRunning", isRunning);
    }

    /// <summary>
    /// Handles jump input from the player.
    /// </summary>
    private void HandleJumpInput()
    {
        if (IsPickLocked())
        {
            return;
        }

        TryJump();
    }

    /// <summary>
    /// Handles interact input (E key) for picking chests or flags.
    /// </summary>
    private void HandleInteractInput()
    {
        if (IsPickLocked())
        {
            return;
        }

        // Try to pick flag first
        TryFindNearbyFlag();
        if (canPickFlag && currentFlag != null)
        {
            HandleFlagPickup();
            return;
        }

        // Then try to pick chest
        TryFindNearbyChest();
        if (!canPickChest)
        {
            return;
        }

        if (heldItem != null)
        {
            return;
        }

        CacheAnimatorReference();

        if (animator != null)
        {
            animator.SetTrigger("Pick");
        }
        
        StartPickLock();
        
        Item newItem = null;
        if (currentChest != null)
        {
            newItem = currentChest.HandlePickStarted(transform);
        }
        RegisterHeldItem(newItem);
    }

    /// <summary>
    /// Handles the flag pickup with animation and rotation.
    /// </summary>
    private void HandleFlagPickup()
    {
        if (IsPickLocked())
        {
            return;
        }

        // Check if player already has a flag
        var teamMember = GetComponent<TeamMember>();
        if (teamMember != null && teamMember.HasFlag)
        {
            return;
        }

        CacheAnimatorReference();

        if (animator != null)
        {
            animator.SetTrigger("Pick");
        }

        StartPickLock();

        if (currentFlag != null)
        {
            currentFlag.PerformPickup();
        }
    }

    /// <summary>
    /// Fallback detection for nearby chests when trigger callbacks are missed.
    /// </summary>
    private void TryFindNearbyChest()
    {
        if (canPickChest && currentChest != null)
        {
            return;
        }

        var hits = Physics.OverlapSphere(transform.position, chestPickupRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            var chest = hit.GetComponentInParent<ChestController>() ?? hit.GetComponent<ChestController>();
            if (chest != null)
            {
                canPickChest = true;
                currentChest = chest;
                return;
            }
        }
    }

    /// <summary>
    /// Fallback detection for nearby flags when trigger callbacks are missed.
    /// </summary>
    private void TryFindNearbyFlag()
    {
        if (canPickFlag && currentFlag != null)
        {
            return;
        }

        var hits = Physics.OverlapSphere(transform.position, flagPickupRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            var flag = hit.GetComponentInParent<FlagTrigger>() ?? hit.GetComponent<FlagTrigger>();
            if (flag == null)
            {
                continue;
            }

            var team = GetComponent<TeamMember>();
            if (team == null || !team.IsOnTeam(flag.Team))
            {
                continue;
            }

            if (team.HasFlag)
            {
                continue;
            }

            canPickFlag = true;
            currentFlag = flag;
            return;
        }
    }


    /// <summary>
    /// Sets the current flag that can be picked up.
    /// </summary>
    /// <param name="flag">The flag trigger to set, or null to clear.</param>
    public void SetCurrentFlag(FlagTrigger flag)
    {
        if (flag != null)
        {
            canPickFlag = true;
            currentFlag = flag;
        }
        else
        {
            canPickFlag = false;
            currentFlag = null;
        }
    }

    /// <summary>
    /// Handles use input (Q key) for consuming held items.
    /// </summary>
    private void HandleUseInput()
    {
        TryConsumeHeldItem();
    }

    /// <summary>
    /// Attempts to make the player jump if grounded and not pick-locked.
    /// </summary>
    private void TryJump()
    {
        if (playerRigidbody == null || IsPickLocked())
        {
            return;
        }

        UpdateGroundStatus();

        if (!isLand)
        {
            return;
        }

        float finalJumpForce = jumpForce;
        if (itemEffectHandler != null)
        {
            finalJumpForce *= itemEffectHandler.CurrentJumpMultiplier;
        }

        playerRigidbody.AddForce(Vector3.up * finalJumpForce, ForceMode.Impulse);

        if (animator != null)
        {
            animator.SetTrigger("Jump");
        }
    }

    /// <summary>
    /// Updates the grounded status and animator.
    /// </summary>
    private void UpdateGroundStatus()
    {
        CheckGround();
        SetGrounded(isGround);
    }

    /// <summary>
    /// Updates the pick lock timer and restores facing direction when complete.
    /// </summary>
    private void UpdatePickLockTimer()
    {
        if (pickLockTimer <= 0f)
        {
            return;
        }

        pickLockTimer = Mathf.Max(0f, pickLockTimer - Time.deltaTime);

        if (pickLockTimer <= 0f)
        {
            RestoreFacingAfterPick();
        }
    }

    /// <summary>
    /// Starts the pick lock timer and faces the player forward.
    /// </summary>
    private void StartPickLock()
    {
        if (!IsPickLocked())
        {
            wasFacingRightBeforePick = isFacingRight;
        }

        pickLockTimer = pickLockDuration;
        shouldRestoreFacingAfterPick = true;
        FaceForwardForPick();
    }

    /// <summary>
    /// Restores the player's facing direction after picking is complete.
    /// </summary>
    private void RestoreFacingAfterPick()
    {
        if (!shouldRestoreFacingAfterPick)
        {
            return;
        }

        if (wasFacingRightBeforePick)
        {
            FaceRight();
        }
        else
        {
            FaceLeft();
        }

        shouldRestoreFacingAfterPick = false;
    }

    /// <summary>
    /// Sets the target rotation to face forward (toward camera) for picking.
    /// </summary>
    private void FaceForwardForPick()
    {
        targetFacingRotation = frontFacingRotation;
    }

    /// <summary>
    /// Attempts to climb a step if there is one in front of the player.
    /// </summary>
    /// <param name="horizontalInputSign">Direction of horizontal movement (-1 or 1).</param>
    private void TryStepClimb(float horizontalInputSign)
    {
        if (Mathf.Approximately(horizontalInputSign, 0f))
        {
            return;
        }

        var moveDirection = new Vector3(horizontalInputSign, 0f, 0f).normalized;
        var origin = transform.position;
        var lowerOrigin = origin + Vector3.up * 0.05f;

        // Check if there's an obstacle at foot level
        if (!Physics.Raycast(lowerOrigin, moveDirection, stepCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        var upperOrigin = lowerOrigin + Vector3.up * stepHeight;

        // Check if there's no obstacle at step height (can step over)
        if (Physics.Raycast(upperOrigin, moveDirection, stepCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        var stepDelta = stepSmoothSpeed * Time.deltaTime;
        transform.position += Vector3.up * stepDelta;
    }

    /// <summary>
    /// Smoothly interpolates the player's rotation toward the target facing direction.
    /// </summary>
    private void SmoothFacingRotation()
    {
        if (rotationSmoothSpeed <= 0f)
        {
            transform.localRotation = targetFacingRotation;
            return;
        }

        var t = rotationSmoothSpeed * Time.deltaTime;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetFacingRotation, t);
    }

    /// <summary>
    /// Checks if the player is currently locked due to picking animation.
    /// </summary>
    /// <returns>True if pick-locked, false otherwise.</returns>
    private bool IsPickLocked()
    {
        return pickLockTimer > 0f;
    }

    /// <summary>
    /// Registers a new held item, discarding the previous one if necessary.
    /// </summary>
    /// <param name="newItem">The new item to hold.</param>
    private void RegisterHeldItem(Item newItem)
    {
        if (newItem == null)
        {
            return;
        }

        if (heldItem != null && heldItem != newItem)
        {
            heldItem.Discard();
            heldItem = null;
            heldItemType = null;
        }

        heldItem = newItem;
        heldItemType = newItem.ItemType;
    }

    /// <summary>
    /// Registers a held item received from network (for non-host clients).
    /// </summary>
    /// <param name="newItem">The item to register.</param>
    public void RegisterHeldItemFromNetwork(Item newItem)
    {
        RegisterHeldItem(newItem);
    }

    /// <summary>
    /// Attempts to consume the currently held item.
    /// </summary>
    private void TryConsumeHeldItem()
    {
        if (heldItem == null)
        {
            return;
        }

        var consumedType = heldItemType;
        heldItem.Consume();
        heldItem = null;
        heldItemType = null;

        ApplyItemEffect(consumedType);
    }

    /// <summary>
    /// Applies the effect of a consumed item through the ItemEffectHandler.
    /// </summary>
    /// <param name="itemType">The type of item consumed.</param>
    private void ApplyItemEffect(string itemType)
    {
        if (itemEffectHandler != null)
        {
            itemEffectHandler.ApplyEffect(itemType);
        }
    }

    /// <summary>
    /// Checks if the player has fallen below the threshold and resets position if needed.
    /// </summary>
    private void CheckFallReset()
    {
        if (transform.position.y >= fallResetThreshold)
        {
            return;
        }

        var floor = FindNearestFloor();
        if (floor == null)
        {
            return;
        }

        var floorPosition = floor.transform.position;
        var targetPosition = new Vector3(floorPosition.x, floorPosition.y + floorSnapHeightOffset, floorPosition.z);
        transform.position = targetPosition;

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Finds the nearest floor object tagged as "Floor".
    /// </summary>
    /// <returns>The nearest floor GameObject, or null if none found.</returns>
    private GameObject FindNearestFloor()
    {
        var floors = GameObject.FindGameObjectsWithTag("Floor");
        if (floors == null || floors.Length == 0)
        {
            return null;
        }

        var currentPosition = transform.position;
        GameObject nearestFloor = null;
        var bestDistanceSquared = float.MaxValue;

        foreach (var floor in floors)
        {
            var delta = floor.transform.position - currentPosition;
            var sqrDistance = delta.sqrMagnitude;

            if (sqrDistance < bestDistanceSquared)
            {
                bestDistanceSquared = sqrDistance;
                nearestFloor = floor;
            }
        }

        return nearestFloor;
    }

    /// <summary>
    /// Handles ESC key input to exit mini game.
    /// </summary>
    private void HandleMiniGameEscapeInput()
    {
        if (!isPlayingMiniGame)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (MiniGameManager.Instance != null)
            {
                MiniGameManager.Instance.ExitCurrentMiniGame();
            }
        }
    }

    /// <summary>
    /// Sets whether the player is currently playing a mini game.
    /// </summary>
    /// <param name="playing">True if playing mini game, false otherwise.</param>
    public void SetPlayingMiniGame(bool playing)
    {
        isPlayingMiniGame = playing;

        // Stop any movement when entering mini game
        if (playing)
        {
            SetRunningAnimation(false);
            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// Gets whether the player is currently playing a mini game.
    /// </summary>
    /// <returns>True if playing mini game, false otherwise.</returns>
    public bool IsPlayingMiniGame()
    {
        return isPlayingMiniGame;
    }

    /// <summary>
    /// Called when a mini game ends with a result.
    /// </summary>
    /// <param name="result">1 = completed, -1 = failed, 0 = exited via ESC</param>
    public void OnMiniGameResult(int result)
    {
        // Handle mini game result here
        // 1 = player completed the game successfully
        // -1 = player failed the game
        // 0 = player exited via ESC
        Debug.Log($"[PlayerController] Mini game ended with result: {result}");
    }

    /// <summary>
    /// Sets the grounded state and updates the animator.
    /// </summary>
    /// <param name="grounded">Whether the player is grounded.</param>
    private void SetGrounded(bool grounded)
    {
        if (isLand == grounded)
        {
            return;
        }

        isLand = grounded;

        if (animator != null)
        {
            animator.SetBool("isLand", grounded);
        }
    }

    /// <summary>
    /// Called when the player enters a trigger collider. Enables chest interaction.
    /// </summary>
    /// <param name="other">The collider that was entered.</param>
    private void OnTriggerEnter(Collider other)
    {
        // Accept either a tagged chest or any collider under a ChestController to avoid tag mismatch issues
        var chest = other.GetComponentInParent<ChestController>();
        if (chest == null)
        {
            chest = other.GetComponent<ChestController>();
        }

        if (chest == null)
        {
            return;
        }

        canPickChest = true;
        currentChest = chest;
    }

    /// <summary>
    /// Called when the player exits a trigger collider. Disables chest interaction.
    /// </summary>
    /// <param name="other">The collider that was exited.</param>
    private void OnTriggerExit(Collider other)
    {
        var chest = other.GetComponentInParent<ChestController>();
        if (chest == null)
        {
            chest = other.GetComponent<ChestController>();
        }

        if (chest == null)
        {
            return;
        }

        if (chest == currentChest)
        {
            canPickChest = false;
            currentChest = null;
        }
    }

    /// <summary>
    /// Checks if the player is touching the ground using a sphere check.
    /// </summary>
    void CheckGround()
    {
        isGround = Physics.CheckSphere(groundCheckPos.position, groundDistance, groundMask);
    }

    /// <summary>
    /// Sets up input actions and event subscriptions for the new Input System.
    /// </summary>
    private void InitializeInputActions()
    {
        if (inputActions != null)
        {
            return;
        }

        inputActions = new InputSystem_Actions();

        moveAction = inputActions.Player.Move;
        jumpAction = inputActions.Player.Jump;
        pickAction = inputActions.Player.Pick;
        useAction = inputActions.Player.Use;

        if (jumpAction != null)
        {
            jumpAction.performed += OnJumpPerformed;
        }

        if (pickAction != null)
        {
            pickAction.performed += OnPickPerformed;
        }

        if (useAction != null)
        {
            useAction.performed += OnUsePerformed;
        }
    }

    /// <summary>
    /// Enables player input actions for the local player.
    /// </summary>
    private void EnablePlayerInput()
    {
        if (inputActions == null)
        {
            return;
        }

        inputActions.Player.Enable();
    }

    /// <summary>
    /// Disables player input actions.
    /// </summary>
    private void DisablePlayerInput()
    {
        if (inputActions == null)
        {
            return;
        }

        inputActions.Player.Disable();
    }

    /// <summary>
    /// Cleans up input action subscriptions and disposes the asset.
    /// </summary>
    private void DisposeInputActions()
    {
        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPerformed;
        }

        if (pickAction != null)
        {
            pickAction.performed -= OnPickPerformed;
        }

        if (useAction != null)
        {
            useAction.performed -= OnUsePerformed;
        }

        inputActions?.Dispose();
        inputActions = null;
        moveAction = null;
        jumpAction = null;
        pickAction = null;
        useAction = null;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (!IsLocalPlayer || isPlayingMiniGame)
        {
            return;
        }

        HandleJumpInput();
    }

    private void OnPickPerformed(InputAction.CallbackContext context)
    {
        if (!IsLocalPlayer || isPlayingMiniGame)
        {
            return;
        }

        HandleInteractInput();
    }

    private void OnUsePerformed(InputAction.CallbackContext context)
    {
        if (!IsLocalPlayer || isPlayingMiniGame)
        {
            return;
        }

        HandleUseInput();
    }
}

