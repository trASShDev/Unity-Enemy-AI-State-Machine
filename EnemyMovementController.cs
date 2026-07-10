using UnityEngine;
using UnityEngine.AI;
public enum AIState { Idle, Follow, Wander, Approach, Flee, Attack, Stay }
public class EnemyMovementAI : MonoBehaviour
{
    [Header("References")]
    public Transform target;

    private NavMeshAgent agent;
    private PlayerAttackController pac;
    private PlayerMovementController pmc;
    private Animator animator;

    [SerializeField] public AIState state = AIState.Idle;

    [Header("Follow Delay (Random)")]
    [SerializeField] private float followDelayMin = 0.3f;
    [SerializeField] private float followDelayMax = 1.5f;

    [Header("Normal (player not attacking/dashing) Weights")]
    [SerializeField] private float wNormalFollow = 80f;
    [SerializeField] private float wNormalStay = 20f;
    [SerializeField] private float wNormalApproach = 15f;

    [Header("Decision Timing")]
    [SerializeField] public float decisionIntervalMin = 0.4f;
    [SerializeField] public float decisionIntervalMax = 1.2f;

    [Header("Combat Distances")]
    [SerializeField] private float attackRange = 2.0f;
    [SerializeField] private float approachStopDistance = 1.5f;
    [SerializeField] private float fleeDistance = 6.0f;

    [Header("Wander")]
    [SerializeField] private float wanderRadius = 6.0f;
    [SerializeField] private float wanderPointReachDistance = 1.0f;

    [Header("Attack Reaction Weights (when player is attacking)")]
    [SerializeField] private float wRunAway = 35f;
    [SerializeField] private float wStay = 20f;
    [SerializeField] private float wAttack = 25f;
    [SerializeField] private float wApproach = 20f;

    [Header("Difficulty / Personality")]
    [Tooltip("Higher = enemy is more likely to commit to attacking/approaching instead of fleeing/staying.")]
    [SerializeField] public float aggressionMultiplier = 1.0f;

    [Tooltip("Higher = enemy changes its mind more often.")]
    [SerializeField] public float reactivenessMultiplier = 1.0f;

    [Header("Baseline Flee (even if player isn't attacking)")]
    [SerializeField, Range(0f, 1f)] public float baselineFleeChance = 0.05f; // 5% per decision
    [SerializeField] private float baselineFleeCooldown = 3f;

    private float nextBaselineFleeAllowedTime;

    [Header("Optional: Strafe / Circle (suggestion)")]
    [SerializeField] private bool enableCircling = true;
    [SerializeField] private float circleRadius = 2.5f;
    [SerializeField] private float circleSpeed = 1.0f;

    private float nextDecisionTime;
    private float followAllowedTime;
    private Vector3 wanderDestination;
    private float circleAngle;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 10f;

    private void FaceTarget()
    {
        if (!target) return;

        Vector3 dir = target.position - agent.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(dir);
        agent.transform.rotation = Quaternion.Slerp(agent.transform.rotation, rot, rotationSpeed * Time.deltaTime);
    }

    private void FaceMovement()
    {
        Vector3 v = agent.desiredVelocity;  
        v.y = 0f;

        if (v.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(v);
        agent.transform.rotation = Quaternion.Slerp(agent.transform.rotation, rot, rotationSpeed * Time.deltaTime);
    }

    void Awake()
    {
        if (target == null)
            target = GameObject.Find("Player").transform;
        agent = GetComponent<NavMeshAgent>();
        pac = FindFirstObjectByType<PlayerAttackController>();
        pmc = FindFirstObjectByType<PlayerMovementController>();
        animator = GetComponent<Animator>();

        
        agent.stoppingDistance = approachStopDistance;
        agent.updateRotation = false;
    }

    void OnEnable()
    {
        ScheduleFollowDelay();
        ScheduleNextDecision();
    }

    void Update()
    {
        //animations
        if (!agent.isStopped) animator.SetBool("IsRunning", true);
        else animator.SetBool("IsRunning", false);


        if (!target || agent == null) return;

        bool playerAttacking = pac != null && pac.isAttacking;
        bool playerDashing = pmc != null && pmc.IsDashing;

        //highest priority: if player is dashing - wander
        if (playerDashing)
        {
            SetState(AIState.Wander);
            TickWander();
            return;
        }

        //if player is attacking - periodically decide reaction
        if (playerAttacking)
        {
            if (Time.time >= nextDecisionTime)
            {
                DecideReactionToAttack();
                ScheduleNextDecision();
            }
        }
        else
        {
            //not attacking, not dashing

            if (Time.time >= nextDecisionTime)
            {
                //not approaching
                if (state == AIState.Approach)
                {
                    ScheduleNextDecision();
                }
                else
                {
                    //rare baseline flee
                    TryBaselineFlee();

                    //if not fleeing, decide follow vs stay
                    if (state != AIState.Flee)
                    {
                        //if can not follow, force stay/idle instead of follow
                        if (Time.time < followAllowedTime)
                        {
                            SetState(AIState.Stay);
                        }
                        else
                        {
                            DecideNormalBehavior();

                            //if follow chosen, immediately start a new cooldown so it doesn't spam follow decisions
                            if (state == AIState.Follow)
                                ScheduleFollowDelay();
                        }
                    }
                }
                ScheduleNextDecision();
            }
        }

        //run state logic
        switch (state)
        {
            case AIState.Idle:
                agent.isStopped = true;
                agent.ResetPath();
                FaceTarget();
                animator.SetBool("IsRunning", false);
                break;

            case AIState.Follow:
                agent.isStopped = false;
                animator.SetBool("IsRunning", true);
                agent.stoppingDistance = approachStopDistance;
                agent.SetDestination(target.position);
                FaceMovement();
                break;

            case AIState.Approach:
                agent.isStopped = false;
                animator.SetBool("IsRunning", true);
                agent.stoppingDistance = approachStopDistance;
                agent.SetDestination(target.position);
                FaceMovement();

                //if close enough, transition to attack
                if (DistanceToTarget() <= attackRange)
                    SetState(AIState.Attack);
                break;

            case AIState.Flee:
                agent.isStopped = false;
                animator.SetBool("IsRunning", true);
                FaceMovement();
                TickFlee();
                break;

            case AIState.Attack:
                TickAttack();
                FaceTarget();
                animator.SetBool("IsRunning", false);
                break;

            case AIState.Stay:
                agent.isStopped = true;
                animator.SetBool("IsRunning", false);
                agent.ResetPath();
                FaceTarget();
                break;

            case AIState.Wander:
                TickWander();
                agent.stoppingDistance = wanderPointReachDistance;
                animator.SetBool("IsRunning", true);
                FaceTarget();
                break;
        }
    }

    private void DecideReactionToAttack()
    {
        //aggression based weights
        float run = wRunAway / Mathf.Max(0.01f, aggressionMultiplier);
        float stay = wStay / Mathf.Max(0.01f, aggressionMultiplier);
        float attack = wAttack * aggressionMultiplier;
        float approach = wApproach * aggressionMultiplier;

        float roll = WeightedRoll(run, stay, attack, approach);

        if (roll < run) SetState(AIState.Flee);
        else if (roll < run + stay) SetState(AIState.Stay);
        else if (roll < run + stay + attack) SetState(AIState.Attack);
        else SetState(AIState.Approach);
    }

    private void TickFlee()
    {
        Vector3 awayDir = (transform.position - target.position).normalized;
        Vector3 desired = transform.position + awayDir * fleeDistance;

        if (TrySetDestinationOnNavMesh(desired, fleeDistance))
        {
            //if there is a path, keep it until reached flee distance from player
            if (DistanceToTarget() >= fleeDistance * 0.9f)
            {
                //after fleeing, reroll a follow delay so it doesn't instantly reengage
                ScheduleFollowDelay();
                SetState(AIState.Idle);
            }
        }
        else
        {
            //if cannot flee properly, fallback to stay
            SetState(AIState.Stay);
        }
    }

    private void TickAttack()
    {
        float dist = DistanceToTarget();

        //if too far, go back to approach
        if (dist > attackRange + 0.5f)
        {
            ScheduleNextDecision();
            SetState(AIState.Idle);
            return;
        }

        //circling
        if (enableCircling)
        {
            circleAngle += circleSpeed * Time.deltaTime;
            Vector3 offset = new Vector3(Mathf.Cos(circleAngle), 0f, Mathf.Sin(circleAngle)) * circleRadius;
            Vector3 circlePoint = target.position + offset;

            if (TrySetDestinationOnNavMesh(circlePoint, circleRadius + 1f))
                return;
        }


        agent.ResetPath();
    }

    private void TickWander()
    {
        //if there is no path pick a new wander destination
        if (!agent.hasPath || agent.remainingDistance <= wanderPointReachDistance)
        {
            Vector3 randomPoint = target.position + Random.insideUnitSphere * wanderRadius;
            randomPoint.y = target.position.y;

            if (TrySetDestinationOnNavMesh(randomPoint, wanderRadius))
                wanderDestination = agent.destination;
        }
    }

    private void SetState(AIState newState)
    {
        if (state == newState) return;

        state = newState;

        //state enter hooks
        if (state == AIState.Wander)
            agent.stoppingDistance = 0f;

        if (state == AIState.Follow || state == AIState.Approach)
            agent.stoppingDistance = approachStopDistance;

        if (state == AIState.Attack)
            agent.stoppingDistance = 0f;

        if (state == AIState.Follow)
            ScheduleFollowDelay();
    }

    private void ScheduleFollowDelay()
    {
        float delay = Random.Range(followDelayMin, followDelayMax);
        followAllowedTime = Time.time + delay;
    }

    private void ScheduleNextDecision()
    {
        float min = decisionIntervalMin / Mathf.Max(0.01f, reactivenessMultiplier);
        float max = decisionIntervalMax / Mathf.Max(0.01f, reactivenessMultiplier);
        nextDecisionTime = Time.time + Random.Range(min, max);
    }

    private float DistanceToTarget()
    {
        return Vector3.Distance(transform.position, target.position);
    }

    private float WeightedRoll(float a, float b, float c, float d)
    {
        float sum = Mathf.Max(0f, a) + Mathf.Max(0f, b) + Mathf.Max(0f, c) + Mathf.Max(0f, d);
        if (sum <= 0.001f) return 0f;

        float r = Random.Range(0f, sum);
        return r;
    }

    private bool TrySetDestinationOnNavMesh(Vector3 point, float sampleRadius)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            return true;
        }
        return false;
    }

    private void TryBaselineFlee()
    {
        if (Time.time < nextBaselineFleeAllowedTime) return;

        if (Random.value <= baselineFleeChance)
        {
            nextBaselineFleeAllowedTime = Time.time + baselineFleeCooldown;
            SetState(AIState.Flee);
            ScheduleNextDecision();
            return;
        }

        ScheduleNextDecision();
    }

    private void DecideNormalBehavior()
    {
        float follow = Mathf.Max(0f, wNormalFollow);
        float stay = Mathf.Max(0f, wNormalStay);
        float approach = Mathf.Max(0f, wNormalApproach) * aggressionMultiplier;

        float sum = follow + stay + approach;
        if (sum <= 0.001f)
        {
            SetState(AIState.Idle);
            return;
        }

        float r = Random.Range(0f, sum);

        if (r < follow)
            SetState(AIState.Follow);
        else if (r < follow + stay)
            SetState(AIState.Stay);
        else
            SetState(AIState.Approach);
    }
}