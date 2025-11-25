using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [SerializeField]
    private float playerSpeed = 5f;

    [SerializeField, Range(0f, 1f)]
    private float inputDeadZone = 0.3f;

    [SerializeField, Range(0f, 1f)]
    private float logChangeThreshold = 0.5f;

    [Header("Jump Settings")]
    [SerializeField]
    private float jumpForce = 6f;

    [Header("Ground Check Settings")]
    public Transform groundCheckPos;
    public LayerMask groundMask;
    public float groundDistance = 0.4f;
    public bool isGround = false;

    [Header("Step Settings")]
    [SerializeField, Range(0.05f, 1f)]
    private float stepHeight = 0.5f;

    [SerializeField, Range(0.05f, 1f)]
    private float stepCheckDistance = 0.3f;

    [SerializeField, Range(0.5f, 10f)]
    private float stepSmoothSpeed = 10f;

    [Header("Facing Settings")]
    [SerializeField, Range(0.1f, 20f)]
    private float rotationSmoothSpeed = 10f;
    
    [Header("Pick Settings")]
    [SerializeField]
    private float pickLockDuration = 1.3f;

    [Header("Fall Reset Settings")]
    [SerializeField]
    private float fallResetThreshold = -20f;

    [SerializeField]
    private float floorSnapHeightOffset = 2f;

    private Vector2 lastLoggedInput = Vector2.zero;
    public Animator animator;
    private Rigidbody playerRigidbody;

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

    private void Awake()
    {
        CacheAnimatorReference();
        CacheRigidbodyReference();
        CacheItemEffectHandler();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CacheFacingRotations();
        IgnorePlayerCollisions();
    }

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

        CheckGround();
        UpdateGroundStatus();
        UpdatePickLockTimer();
        CheckFallReset();

        var inputValue = new Vector2(Input.GetAxis("Horizontal"), 0f);

        if (HasSignificantInputChange(inputValue))
        {
            lastLoggedInput = inputValue;
        }

        var horizontalInput = Mathf.Abs(inputValue.x) < inputDeadZone ? 0f : inputValue.x;
        var pickLocked = IsPickLocked();
        var horizontal = pickLocked ? 0f : horizontalInput;

        UpdateFacingDirection(horizontalInput);

        var hasMovement = !Mathf.Approximately(horizontal, 0f);

        SetRunningAnimation(hasMovement);

        HandleJumpInput();
        HandleInteractInput();
        HandleUseInput();

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

    private bool HasSignificantInputChange(Vector2 current)
    {
        return Vector2.Distance(current, lastLoggedInput) >= logChangeThreshold;
    }

    private void CacheAnimatorReference()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void CacheRigidbodyReference()
    {
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody>();
        }
    }

    private void CacheItemEffectHandler()
    {
        if (itemEffectHandler == null)
        {
            itemEffectHandler = GetComponent<ItemEffectHandler>();
        }
    }

    private void CacheFacingRotations()
    {
        rightFacingRotation = transform.localRotation;
        leftFacingRotation = transform.localRotation * Quaternion.Euler(0f, 180f, 0f);
        frontFacingRotation = transform.localRotation * Quaternion.Euler(0f, -90f, 0f);
        targetFacingRotation = transform.localRotation;
    }

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

    private void FaceRight()
    {
        targetFacingRotation = rightFacingRotation;
        isFacingRight = true;
    }

    private void FaceLeft()
    {
        targetFacingRotation = leftFacingRotation;
        isFacingRight = false;
    }

    private void SetRunningAnimation(bool isRunning)
    {
        CacheAnimatorReference();

        if (animator == null)
        {
            return;
        }

        animator.SetBool("isRunning", isRunning);
    }

    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (IsPickLocked())
            {
                return;
            }

            TryJump();
        }
    }

    private void HandleInteractInput()
    {
        if (!Input.GetKeyDown(KeyCode.E))
        {
            return;
        }

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

    private void HandleUseInput()
    {
        if (!Input.GetKeyDown(KeyCode.Q))
        {
            return;
        }

        TryConsumeHeldItem();
    }

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

        playerRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        if (animator != null)
        {
            animator.SetTrigger("Jump");
        }
    }

    private void UpdateGroundStatus()
    {
        CheckGround();
        SetGrounded(isGround);
    }
    
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

    private void FaceForwardForPick()
    {
        targetFacingRotation = frontFacingRotation;
    }

    private void TryStepClimb(float horizontalInputSign)
    {
        if (Mathf.Approximately(horizontalInputSign, 0f))
        {
            return;
        }

        var moveDirection = new Vector3(horizontalInputSign, 0f, 0f).normalized;
        var origin = transform.position;
        var lowerOrigin = origin + Vector3.up * 0.05f;

        if (!Physics.Raycast(lowerOrigin, moveDirection, stepCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        var upperOrigin = lowerOrigin + Vector3.up * stepHeight;

        if (Physics.Raycast(upperOrigin, moveDirection, stepCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        var stepDelta = stepSmoothSpeed * Time.deltaTime;
        transform.position += Vector3.up * stepDelta;
    }

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

    private bool IsPickLocked()
    {
        return pickLockTimer > 0f;
    }

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

    private void ApplyItemEffect(string itemType)
    {
        if (itemEffectHandler != null)
        {
            itemEffectHandler.ApplyEffect(itemType);
        }
    }

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

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Chest"))
        {
            return;
        }

        canPickChest = true;
        var chest = other.GetComponentInParent<ChestController>();
        if (chest == null)
        {
            chest = other.GetComponent<ChestController>();
        }
        if (chest != null)
        {
            currentChest = chest;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Chest"))
        {
            return;
        }

        canPickChest = false;

        var chest = other.GetComponentInParent<ChestController>();
        if (chest == null)
        {
            chest = other.GetComponent<ChestController>();
        }
        if (chest != null && chest == currentChest)
        {
            currentChest = null;
        }
    }

    void CheckGround()
    {
        isGround = Physics.CheckSphere(groundCheckPos.position, groundDistance, groundMask);
    }
}

