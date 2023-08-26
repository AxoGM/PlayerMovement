using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Drawing;
using UnityEngine;

public class PlayerControl : MonoBehaviour
{
    Rigidbody rb;
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        readyToJump = true;
        startYScale = transform.localScale.y;
        startYScale = playerObj.localScale.y;
    }
    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask isGround;
    public bool grounded;
    private void Update()
    {
        // player check if it ground or not
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, isGround);
        MyInput();
        SpeedControl();
        StateHandler();
        CheckForWall();
        StateMachine();
        WallCheck();
        if (grounded && state == MovementState.walking || state == MovementState.sprinting || state == MovementState.crouching)
            rb.drag = groundDrag;
        else
            rb.drag = 0;
        // climb update
        if (climbing && !exitingWall) ClimbingMovement();
        // dash update
        if (Input.GetKeyDown(dashKey))
            Dash();
        if (dashCdTimer > 0)
            dashCdTimer -= Time.deltaTime;
    }
    [Header("Movement")]
    public float walkSpeed;
    public float sprintSpeed;
    public float slideSpeed;
    public float wallrunSpeed;
    public float dashSpeed;
    private float moveSpeed;
    public float speedIncreaseMultiplier;
    public float slopeIncreaseMultiplier;
    public float dashSpeedChangeFactor;
    public float maxYSpeed;
    [Header("References")]
    public Transform orientation;
    public Transform playerCam;
    public Transform playerObj;
    public CamLook cam;
    [Header("Input")]
    float xInput;
    float zInput;
    Vector3 Velocity;
    public float groundDrag;
    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.C;
    public KeyCode slideKey = KeyCode.LeftControl;
    public KeyCode upwardsRunKey = KeyCode.LeftShift;
    public KeyCode downwardsRunKey = KeyCode.LeftControl;
    public KeyCode dashKey = KeyCode.E;
    [Header("Crouching")]
    public float crouchSpeed;
    public float crouchYScale;
    private float startYScale;
    // main input
    private void MyInput()
    {
        // movement controller
        xInput = Input.GetAxisRaw("Horizontal");
        zInput = Input.GetAxisRaw("Vertical");
        // jump controller
        if (Input.GetKey(jumpKey) && readyToJump && grounded)
        {
            readyToJump = false;
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown);
        }
        // start crouching
        if (Input.GetKeyDown(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }
        // stop crouching
        if (Input.GetKeyUp(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
        // start sliding
        if (Input.GetKeyDown(slideKey) && (xInput != 0 || zInput != 0))
            StartSlide();
        // stop sliding
        if (Input.GetKeyUp(slideKey) && sliding)
            StopSlide();
    }
    private void FixedUpdate()
    {
        PlayerMove();
        if (sliding)
            SlidingMovement();
        if (wallrunning)
            WallRunningMovement();
    }
    public MovementState state;
    // movement list
    public enum MovementState
    {
        walking,
        sprinting,
        crouching,
        sliding,
        wallrunning,
        climbing,
        dashing,
        air
    }
    private bool sliding;
    private bool wallrunning;
    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    private MovementState lastState;
    private bool keepMomentum;
    // state handle
    private void StateHandler()
    {
        // dashing
        if (dashing)
        {
            state = MovementState.dashing;
            desiredMoveSpeed = dashSpeed;
            speedChangeFactor = dashSpeedChangeFactor;
        }
        // climbing
        else if (climbing)
        {
            state = MovementState.climbing;
            desiredMoveSpeed = climbSpeed;
        }
        // wallrunning
        else if (wallrunning)
        {
            state = MovementState.wallrunning;
            desiredMoveSpeed = wallrunSpeed;
        }
        // sliding
        else if (sliding)
        {
            state = MovementState.sliding;
            if (OnSlope() && rb.velocity.y < 0.1f)
                desiredMoveSpeed = slideSpeed;
            else
                desiredMoveSpeed = sprintSpeed;
        }
        // crouching
        else if (Input.GetKey(crouchKey))
        {
            state = MovementState.crouching;
            desiredMoveSpeed = crouchSpeed;
        }
        // sprintning
        else if (grounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
            desiredMoveSpeed = sprintSpeed;
        }
        // walking
        else if (grounded)
        {
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }
        // air
        else
        {
            state = MovementState.air;
            if (desiredMoveSpeed < sprintSpeed)
                desiredMoveSpeed = walkSpeed;
            else
                desiredMoveSpeed = sprintSpeed;
        }
        lastDesiredMoveSpeed = desiredMoveSpeed;
        lastState = state;
        bool desiredMoveSpeedHasChange = desiredMoveSpeed != lastDesiredMoveSpeed;
        if (lastState == MovementState.dashing) keepMomentum = true;
        if (desiredMoveSpeedHasChange)
        {
            if (keepMomentum)
            {
                StopAllCoroutines();
                SmothlyLerpMoveSpeed();
            }
            else
            {
                StopAllCoroutines();
                moveSpeed = desiredMoveSpeed;
            }
        }
        if (Mathf.Abs(desiredMoveSpeed - lastDesiredMoveSpeed) > 4f && moveSpeed != 0)
        {
            StopAllCoroutines();
            SmothlyLerpMoveSpeed();
        }
        else
        {
            moveSpeed = desiredMoveSpeed;
        }
    }
    private float speedChangeFactor;
    // smothly move speed
    private IEnumerable SmothlyLerpMoveSpeed()
    {
        float time = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
        float startValue = moveSpeed;
        float boostFactor = speedChangeFactor;
        while (time < difference)
        {
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);
            time += Time.deltaTime * boostFactor;
            if (OnSlope())
            {
                float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
                float slopeAngleIncrease = 1 + (slopeAngle / 90f);
                time += Time.deltaTime * speedIncreaseMultiplier * slopeIncreaseMultiplier * slopeAngleIncrease;
            }
            else
                time += Time.deltaTime * speedIncreaseMultiplier;
            yield return null;
        }
        moveSpeed = desiredMoveSpeed;
        speedChangeFactor = 1f;
        keepMomentum = false;
    }
    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;
    // slope mode
    public bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }
    // player movement input
    private void PlayerMove()
    {
        if (state == MovementState.dashing) return;
        if (exitingWall) return;
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(SlopeMoveDir(Velocity) * moveSpeed * 20f, ForceMode.Force);
            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
        Velocity = orientation.forward * zInput + orientation.right * xInput;
        if (grounded)
            rb.AddForce(Velocity.normalized * moveSpeed * 10f, ForceMode.Force);
        else if (!grounded)
            rb.AddForce(Velocity.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        if (!wallrunning) rb.useGravity = !OnSlope();
    }
    public Vector3 SlopeMoveDir(Vector3 dir)
    {
        return Vector3.ProjectOnPlane(dir, slopeHit.normal).normalized;
    }
    // speed control
    private void SpeedControl()
    {
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }
        else
        {
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitVel.x, rb.velocity.y, limitVel.z);
            }
        }
        // limit y jump
        if (maxYSpeed != 0 && rb.velocity.y > maxYSpeed)
            rb.velocity = new Vector3(rb.velocity.x, maxYSpeed, rb.velocity.z);
    }
    [Header("Jump")]
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    bool readyToJump;
    // jump input
    private void Jump()
    {
        exitingSlope = true;
        // jump input
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }
    // reset jump
    private void ResetJump()
    {
        readyToJump = true;
        exitingSlope = false;
    }
    [Header("Sliding")]
    public float maxSlideTime;
    public float slideForce;
    private float slideTimer;
    public float slideYScale;
    // start slide
    private void StartSlide()
    {
        sliding = true;
        playerObj.localScale = new Vector3(playerObj.localScale.x, slideYScale, playerObj.localScale.z);
        rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        slideTimer = maxSlideTime;
    }
    // slide input
    private void SlidingMovement()
    {
        Vector3 inputDir = orientation.forward * zInput + orientation.right * zInput;
        if (!OnSlope() || rb.velocity.y > -0.1f)
        {
            rb.AddForce(inputDir.normalized * slideForce, ForceMode.Force);
            slideForce -= Time.deltaTime;
        }
        else
        {
             rb.AddForce(SlopeMoveDir(inputDir) * slideForce, ForceMode.Force);
        }
        if (slideTimer <= 0)
            StopSlide();
    }
    // stop slide
    private void StopSlide()
    {
        sliding = false;
        playerObj.localScale = new Vector3(playerObj.localScale.x, startYScale, playerObj.localScale.z);
    }
    [Header("Wallrunning")]
    public LayerMask isWall;
    public float wallRunForce;
    public float maxWallRunTime;
    private float wallRunTimer;
    public float wallClimbSpeed;
    private bool upwardsRunning;
    private bool downwardsRunning;
    public float wallJumpUpForce;
    public float wallJumpSideForce;
    [Header("Detection")]
    public float wallCheckDistance;
    public float minJumpHeight;
    private RaycastHit leftWallhit;
    private RaycastHit rightWallhit;
    private bool wallLeft;
    private bool wallRight;
    public float detectionLength;
    public float sphereCastRadius;
    public float maxWallLookAngle;
    private float wallLookAngle;
    private RaycastHit frontWallHit;
    private bool wallFront;
    private Transform lastWall;
    private Vector3 lastWallNormal;
    public float minWallNormalAngleChange;
    // player check for wall
    private void CheckForWall()
    {
        // wall check right
        wallRight = Physics.Raycast(transform.position, orientation.right, out rightWallhit, wallCheckDistance, isWall);
        // wall check left
        wallLeft = Physics.Raycast(transform.position, -orientation.right, out leftWallhit, wallCheckDistance, isWall);
    }
    private bool AboveGround()
    {
        return !Physics.Raycast(transform.position, Vector3.down, minJumpHeight, isGround);
    }
    [Header("Exiting")]
    public float exitWallTime;
    private float exitWallTimer;
    public bool exitingWall;
    // state machine
    private void StateMachine()
    {
        xInput = Input.GetAxisRaw("Horizontal");
        zInput = Input.GetAxisRaw("Vertical");
        upwardsRunning = Input.GetKey(upwardsRunKey);
        downwardsRunning = Input.GetKey(downwardsRunKey);
        if ((wallLeft || wallRight) && zInput > 0 && AboveGround() && !exitingWall)
        {
            if (!wallrunning)
                StartWallRun();
            // wallrun timer
            if (wallRunTimer > 0)
                wallRunTimer -= Time.deltaTime;
            if (wallRunTimer <= 0 && wallrunning)
            {
                exitingWall = true;
                exitWallTimer = exitWallTime;
            }
            // wall jump
            if (Input.GetKeyDown(jumpKey)) WallJump();
        }
        else if (exitingWall)
        {
            if (wallrunning)
                StopWallRun();
            if (exitWallTimer > 0)
                exitWallTimer -= Time.deltaTime;
            if (exitWallTimer <= 0)
                exitingWall = false;
        }
        else
        {
            if (wallrunning)
                StopWallRun();
        }
        // climb input for state maachine
        if (wallFront && Input.GetKey(KeyCode.W) && wallLookAngle < maxWallLookAngle && !exitingWall)
        {
            if (!climbing && climbTimer > 0) StartClimbing();
            if (climbTimer > 0) climbTimer -= Time.deltaTime;
            if (climbTimer < 0) StopClimbing();
        }
        else if (exitingWall)
        {
            if (climbing) StopClimbing();
            if (exitWallTimer > 0) exitWallTimer -= Time.deltaTime;
            if (exitWallTimer < 0) exitingWall = false;
        }
        else
        {
            if (climbing) StopClimbing();
        }
        // climb jump input
        if (wallFront && Input.GetKeyDown(jumpKey) && climbJumpLeft > 0) ClimbJump();
    }
    // start wallrun
    private void StartWallRun()
    {
        wallrunning = true;
        wallRunTimer = maxWallRunTime;
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        // fov changes and cam tilt active mode
        cam.DoFov(90f);
        if (wallLeft) cam.DoTilt(-5f);
        if (wallRight) cam.DoTilt(5f);
    }
    [Header("Gravity")]
    public bool useGravity;
    public float gravityCounterForce;
    // wallrun input
    private void WallRunningMovement()
    {
        rb.useGravity = useGravity;
        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        Vector3 wallForward = Vector3.Cross(wallNormal, transform.up);
        if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
            wallForward = -wallForward;
        // force wallrun
        rb.AddForce(wallForward * wallRunForce, ForceMode.Force);
        if (upwardsRunning)
            rb.velocity = new Vector3(rb.velocity.x, wallClimbSpeed, rb.velocity.z);
        if (downwardsRunning)
            rb.velocity = new Vector3(rb.velocity.x, -wallClimbSpeed, rb.velocity.z);
        if (!(wallLeft && xInput > 0) && !(wallRight && xInput < 0))
            rb.AddForce(-wallNormal * 100, ForceMode.Force);
        // wallrun gravity
        if (useGravity)
            rb.AddForce(transform.up * gravityCounterForce, ForceMode.Force);
    }
    // stop wallrun
    private void StopWallRun()
    {
        wallrunning = false;
        // fov changes and cam tilt deactive mode
        cam.DoFov(80f);
        cam.DoTilt(0f);
    }
    // wall jump
    private void WallJump()
    {
        exitingWall = true;
        exitWallTimer = exitWallTime;
        Vector3 wallNormal = wallRight ? rightWallhit.normal : leftWallhit.normal;
        Vector3 forceToApply = transform.up * wallJumpUpForce + wallNormal * wallJumpSideForce;
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(forceToApply, ForceMode.Impulse);
    }
    [Header("Climbing")]
    public LayerMask isWallClimb;
    public float climbSpeed;
    public float maxClimbTime;
    private float climbTimer;
    private bool climbing;
    // wall check
    private void WallCheck()
    {
        wallFront = Physics.SphereCast(transform.position, sphereCastRadius, orientation.forward, out frontWallHit, detectionLength, isWallClimb);
        wallLookAngle = Vector3.Angle(orientation.forward, -frontWallHit.normal);
        bool newWall = frontWallHit.transform != lastWall || Mathf.Abs(Vector3.Angle(lastWallNormal, frontWallHit.normal)) > minWallNormalAngleChange;
        if ((wallFront && newWall) || grounded)
        {
            climbTimer = maxClimbTime;
            climbJumpLeft = climbJump;
        }
    }
    // start climb
    private void StartClimbing()
    {
        climbing = true;
        lastWall = frontWallHit.transform;
        lastWallNormal = frontWallHit.normal;
    }
    // climb input
    private void ClimbingMovement()
    {
        rb.velocity = new Vector3(rb.velocity.x, climbSpeed, rb.velocity.z);
    }
    // stop climb
    private void StopClimbing()
    {
        climbing = false;
    }
    [Header("Climb Jumping")]
    public float climbJumpUpForce;
    public float climbJumpBackForce;
    public int climbJump;
    private int climbJumpLeft;
    // climb jump
    private void ClimbJump()
    {
        exitingWall = true;
        exitWallTimer = exitWallTime;
        Vector3 forceToApply = transform.up * climbJumpUpForce + frontWallHit.normal * climbJumpBackForce;
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(forceToApply, ForceMode.Impulse);
        climbJumpLeft--;
    }
    [Header("Dashing")]
    public float dashForce;
    public float dashUpwardForce;
    public float dashDuration;
    public float maxDashYSpeed;
    public bool dashing;
    [Header("Cooldown")]
    public float dashCd;
    private float dashCdTimer;
    // dash input
    private void Dash()
    {
        if (dashCdTimer > 0) return;
        else dashCdTimer = dashCd;
        dashing = true;
        maxYSpeed = maxDashYSpeed;
        cam.DoFov(dashFov);
        Transform forwardT;
        if (useCamForward)
            forwardT = playerCam;
        else
            forwardT = orientation;
        Vector3 dir = GetDir(forwardT);
        Vector3 forceToApply = dir * dashForce + orientation.up * dashUpwardForce;
        if (disableGravity)
            rb.useGravity = false;
        rb.AddForce(forceToApply, ForceMode.Impulse);
        delayForceToApply = forceToApply;
        Invoke(nameof(DelayDashForce), 0.025f);
        Invoke(nameof(ResetDash), dashDuration);
    }
    private Vector3 delayForceToApply;
    // delay dash
    private void DelayDashForce()
    {
        if (resetVel)
            rb.velocity = Vector3.zero;
        rb.AddForce(delayForceToApply, ForceMode.Impulse);
    }
    // reset dash
    private void ResetDash()
    {
        dashing = false;
        maxYSpeed = 0;
        cam.DoFov(80f);
        if (disableGravity)
            rb.useGravity = true;
    }
    private Vector3 GetDir(Transform forwardT)
    {
        float xInput = Input.GetAxisRaw("Horizontal");
        float zInput = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3();
        if (allowAllDir)
            dir = forwardT.forward * zInput + forwardT.right * xInput;
        else
            dir = forwardT.forward;
        if (zInput == 0 && xInput == 0)
            dir = forwardT.forward;
        return dir.normalized;
    }
    [Header("Settings")]
    public bool useCamForward = true;
    public bool allowAllDir = true;
    public bool disableGravity = false;
    public bool resetVel = true;
    [Header("Camera Effects")]
    public float dashFov;
}
