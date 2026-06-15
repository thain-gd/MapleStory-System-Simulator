using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MSSim.Constants;

public class PlayerMovement : MonoBehaviour
{
	#region Serialize Fields
	[Header("Movement Values")]
	[SerializeField] float movingSpeed = 5f;
	[SerializeField] float climbingSpeed = 5f;
	[SerializeField] float jumpForce = 10f;
	[SerializeField] float jumpAsideForceX = 4f;
	[SerializeField] float jumpAsideForceY = 7f;

	[Header("Ground Check Info")]
	[SerializeField] LayerMask groundMask;
	[SerializeField] bool grounded;


	[Header("Others")]
	[SerializeField] float raycastDistance = 5f;
	[SerializeField] LayerMask ladderMask;
	[SerializeField] Vector3 raycastOffset = new Vector3(-0.6f, -1.65f);
	#endregion

	#region Class Fields
	private Rigidbody2D rigidBody2D;
	private BoxCollider2D playerCollider;
	private Animator animator;
	private bool facingRight;

    private enum PlayerState
    {
        IDLE,
        WALK,
        JUMP,
        JUMP_DOWN,
        JUMP_ASIDE,
        CLIMB
    }
    private PlayerState state;

	private RaycastHit2D hitLadderUp;
	private RaycastHit2D hitLadderDown;
	private bool isInLadderArea;

	private const string IS_WALKING_STRING = "isWalking";
	private const string IS_CLIMBING_STRING = "isClimbing";
	#endregion

	#region Methods
	// Start is called before the first frame update
	private void Start()
	{
		animator = GetComponent<Animator>();
		rigidBody2D = GetComponent<Rigidbody2D>();
		playerCollider = GetComponent<BoxCollider2D>();

        state = PlayerState.IDLE;
	}

	// Update is called once per frame
	private void Update()
	{
		CheckGrounded();
		CheckInputs();
	}

	private void CheckGrounded()
	{
		RaycastHit2D hitGround = Physics2D.BoxCast
		(
			playerCollider.bounds.center,
			playerCollider.bounds.size,
			0f,
			Vector2.down,
			PlayerConstants.GroundBoxCastDistance,
			groundMask
		);
		grounded = hitGround.collider != null;
	}

	private void CheckInputs()
	{
        switch (state)
        {
            case PlayerState.IDLE:
                rigidBody2D.bodyType = RigidbodyType2D.Dynamic;
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
                {
                    state = PlayerState.WALK;
                }
                else if (IsClimbable())
                {
                    state = PlayerState.CLIMB;
                }
                else if (grounded)
                {
                    if (Input.GetKey(KeyCode.Space))
                    {
                        if (Input.GetKey(KeyCode.DownArrow))
                        {
                            state = PlayerState.JUMP_DOWN;
                        }
                        else
                        {
                            state = PlayerState.JUMP;
                        }
                    }
                }
                break;

            case PlayerState.WALK:
                if (!Input.GetKey(KeyCode.LeftArrow) && !Input.GetKey(KeyCode.RightArrow))
                {
                    state = PlayerState.IDLE;
                }
                else if (IsClimbable())
                {
                    state = PlayerState.CLIMB;
                }
                else if (grounded)
                {
                    if (Input.GetKey(KeyCode.Space))
                    {
                        if (Input.GetKey(KeyCode.DownArrow))
                        {
                            state = PlayerState.JUMP_DOWN;
                        }
                        else
                        {
                            state = PlayerState.JUMP;
                        }
                    }
                }
                
                break;

            case PlayerState.CLIMB:
                if ((Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)) && Input.GetKey(KeyCode.Space))
                {
                    state = PlayerState.JUMP_ASIDE;
                }
                else if (IsGroundedWhileClimbing() || !isInLadderArea)
                {
                    state = PlayerState.IDLE;
                }
                break;
        }
	}

	private bool IsClimbable() 
	{
		hitLadderUp = Physics2D.Raycast
		(
			playerCollider.bounds.center,
			Vector2.up,
			playerCollider.bounds.size.y / 2,
			ladderMask
		);

		hitLadderDown = Physics2D.Raycast
		(
			playerCollider.bounds.center,
			Vector2.down,
			playerCollider.bounds.size.y / 2 + PlayerConstants.LadderDownRaycastDistanceOffset,
			ladderMask
		);

		if (CanClimbInLadderArea() || CanClimbFromTopOfLadder())
		{
			return true;
		}
		
		return false;
	}

	private bool CanClimbInLadderArea()
	{
		if (Input.GetKey(KeyCode.UpArrow) || (Input.GetKey(KeyCode.DownArrow) && !grounded))
		{
			if (isInLadderArea && (hitLadderUp.collider != null || hitLadderDown.collider != null))
			{
				return true;
			}
		}

		return false;
	}

	private  bool CanClimbFromTopOfLadder() {
		return Input.GetKey(KeyCode.DownArrow) && hitLadderDown.collider != null;
	}

	private bool IsGroundedWhileClimbing()
	{
		return hitLadderDown.collider == null && grounded;
	}

	private void FixedUpdate()
	{
        switch (state)
        {
            case PlayerState.WALK:
                Walk();
                break;

            case PlayerState.JUMP:
                Jump();
                state = PlayerState.IDLE;
                break;

            case PlayerState.JUMP_DOWN:
                StartCoroutine(JumpDown());
                state = PlayerState.IDLE;
                break;

            case PlayerState.JUMP_ASIDE:
                JumpAside();
                state = PlayerState.IDLE;
                break;

            case PlayerState.CLIMB:
                MovePlayerPosToMiddleOfLadder();
                AdjustRigidBodyToClimb();
                Climb();
                break;
        }
	}

	private void Walk()
	{
		float horizontal = Input.GetAxisRaw("Horizontal");
		if ((horizontal > 0 && !facingRight) || (horizontal < 0 && facingRight))
		{
			ChangeDirection();
		}

		Vector3 movingDistance = new Vector3(horizontal * movingSpeed * Time.deltaTime, 0);
		transform.Translate(movingDistance);
	}

	private void ChangeDirection()
	{
		facingRight = !facingRight;
		transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y);
		raycastOffset.x *= -1;
	}

	private void Jump()
	{
		rigidBody2D.linearVelocity = new Vector2(rigidBody2D.linearVelocity.x, jumpForce);
	}

	private IEnumerator JumpDown()
	{
		playerCollider.isTrigger = true;
		yield return new WaitForSeconds(0.5f);
		playerCollider.isTrigger = false;
	}

	private void JumpAside()
	{
        float horizontal = Input.GetAxisRaw("Horizontal");
		rigidBody2D.linearVelocity = horizontal > 0 ? new Vector2(jumpAsideForceX, jumpAsideForceY) : new Vector2(-jumpAsideForceX, jumpAsideForceY);
    }

    private void MovePlayerPosToMiddleOfLadder()
	{
		Vector2 playerPosPriorClimbing = transform.position;
		if (hitLadderUp.collider != null)
		{
			playerPosPriorClimbing = new Vector2(hitLadderUp.collider.bounds.center.x, playerPosPriorClimbing.y);
		}
		else if (hitLadderDown.collider != null)
		{
			playerPosPriorClimbing = new Vector2(hitLadderDown.collider.bounds.center.x, playerPosPriorClimbing.y);
		}

		transform.position = playerPosPriorClimbing;
	}

	private void AdjustRigidBodyToClimb()
	{
		rigidBody2D.linearVelocity = Vector2.zero;
		rigidBody2D.bodyType = RigidbodyType2D.Kinematic;
	}

	private void Climb()
	{
		float vertical = Input.GetAxisRaw("Vertical");
		Vector3 climbingDistance = new Vector3(0, vertical * climbingSpeed * Time.deltaTime);
		transform.Translate(climbingDistance);
	}
	#endregion

	#region Unity Event Methods
	private void OnTriggerEnter2D(Collider2D otherCollider)
	{
		if (otherCollider.tag == "Ladder")
		{
			isInLadderArea = true;
		}
	}

	private void OnTriggerExit2D(Collider2D otherCollider)
	{
		if (otherCollider.tag == "Ladder")
		{
			isInLadderArea = false;
		}
	}
	#endregion
}
