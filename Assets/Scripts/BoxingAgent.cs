using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Analytics;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class BoxingAgent : Agent
{
    [Header("Agent References")]
    public Transform opponent;
    public BoxingArenaManager arenaManager;

    [Header("Body Parts")]
    public Transform head;
    public Transform body;
    public Transform glove_L;
    public Transform glove_R;

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float rotationSpeed = 180f;
    [Tooltip("Jeśli model jest odwrócony, ustaw na -1")]
    public float directionMultiplier = -1f;

    [Header("Combat Settings")]
    public float maxHealth = 100f;
    public float stunDuration = 1f;
    public float knockbackForce = 5f;
    public float ArenaDmg = 10f;

    [Header("Simplified Reward Settings")]
    public float hitReward = 2.0f;          // Nagroda za trafienie
    public float hitPenalty = -1.0f;        // Kara za otrzymanie ciosu
    public float defenseReward = 1.0f;      // Nagroda za obronę
    public float winReward = 10.0f;         // Nagroda za wygraną
    public float lossReward = -5.0f;        // Kara za przegraną
    public float drawPenalty = -2.0f;       // Kara za remis
    public float arenaExitPenalty = -5.0f;  // Kara za wyjście z areny

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Animation trigger names (takie same jak w TestAnimation)
    private readonly string[] animationTriggers = {
        "", // 0 - puste
        "Left_Head_Punch",   // 1
        "Left_Body_Punch",   // 2
        "Right_Head_Punch",  // 3
        "Right_Body_Punch",  // 4
        "Body_Block",        // 5
        "Head_Block"         // 6
    };

    // Simplified combat tracking
    private bool isCurrentlyAttacking = false;
    private int currentAttackType = 0;
    private Coroutine attackCoroutine;

    // Private variables
    private Rigidbody agentRigidbody;
    private Animator animator;
    private float currentHealth;
    private bool isStunned = false;
    private bool isDefending = false;
    private float stunTimer = 0f;
    private int lastAction = 0;
    private float episodeTimer = 0f;
    private const float maxEpisodeTime = 60f;

    // Simple state tracking
    private bool canAct = true;
    private float actionCooldown = 0f;
    private const float attackDuration = 0.5f;

    // Basic statistics
    private int totalEpisodes = 0;
    private int wins = 0;
    private int losses = 0;
    private int draws = 0;

    // Detailed episode statistics for TensorBoard
    private int episodeHits = 0;
    private int episodeMissedAttacks = 0;
    private int episodeAttacks = 0;
    private int episodeDefenseAttempts = 0;
    private int episodeSuccessfulDefenses = 0;
    private int totalHits = 0;
    private int totalMissedAttacks = 0;
    private int totalAttacks = 0;
    private int totalDefenseAttempts = 0;
    private int totalSuccessfulDefenses = 0;

    // Collision detection components
    private Collider headCollider;
    private Collider bodyCollider;
    private Collider gloveLeftCollider;
    private Collider gloveRightCollider;

    // Nie potrzebujemy osobnych zmiennych dla BodyPartCollision,
    // są one dodawane automatycznie jako komponenty

    public override void Initialize()
    {
        agentRigidbody = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        // Setup collision detection
        SetupCollisionDetection();

        // Sprawdź czy wszystkie komponenty są przypisane
        ValidateComponents();

        Debug.Log($"{gameObject.name}: BoxingAgent zainicjalizowany - wszystkie komponenty OK");
    }

    private void SetupCollisionDetection()
    {
        Debug.Log($"{gameObject.name}: Rozpoczynanie konfiguracji detekcji kolizji...");

        // Znajdź collidery części ciała
        if (head != null)
        {
            headCollider = head.GetComponent<Collider>();
            if (headCollider != null)
            {
                Debug.Log($"{gameObject.name}: Znaleziono collider dla głowy: {headCollider.name}, isTrigger: {headCollider.isTrigger}");

                // Dodaj komponent do detekcji kolizji jeśli nie istnieje
                BodyPartCollision headCollisionDetector = head.GetComponent<BodyPartCollision>();
                if (headCollisionDetector == null)
                {
                    headCollisionDetector = head.gameObject.AddComponent<BodyPartCollision>();
                    Debug.Log($"{gameObject.name}: Dodano BodyPartCollision do głowy");
                }
                headCollisionDetector.Initialize(this, BodyPartType.Head);
            }
            else
            {
                Debug.LogError($"{gameObject.name}: Brak collidera na głowie!");
            }
        }

        if (body != null)
        {
            bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
            {
                Debug.Log($"{gameObject.name}: Znaleziono collider dla ciała: {bodyCollider.name}, isTrigger: {bodyCollider.isTrigger}");

                BodyPartCollision bodyCollisionDetector = body.GetComponent<BodyPartCollision>();
                if (bodyCollisionDetector == null)
                {
                    bodyCollisionDetector = body.gameObject.AddComponent<BodyPartCollision>();
                    Debug.Log($"{gameObject.name}: Dodano BodyPartCollision do ciała");
                }
                bodyCollisionDetector.Initialize(this, BodyPartType.Body);
            }
            else
            {
                Debug.LogError($"{gameObject.name}: Brak collidera na ciele!");
            }
        }

        if (glove_L != null)
        {
            gloveLeftCollider = glove_L.GetComponent<Collider>();
            if (gloveLeftCollider != null)
            {
                Debug.Log($"{gameObject.name}: Znaleziono collider dla lewej rękawicy: {gloveLeftCollider.name}, isTrigger: {gloveLeftCollider.isTrigger}");

                BodyPartCollision gloveLeftCollisionDetector = glove_L.GetComponent<BodyPartCollision>();
                if (gloveLeftCollisionDetector == null)
                {
                    gloveLeftCollisionDetector = glove_L.gameObject.AddComponent<BodyPartCollision>();
                    Debug.Log($"{gameObject.name}: Dodano BodyPartCollision do lewej rękawicy");
                }
                gloveLeftCollisionDetector.Initialize(this, BodyPartType.LeftGlove);
            }
            else
            {
                Debug.LogError($"{gameObject.name}: Brak collidera na lewej rękawicy!");
            }
        }

        if (glove_R != null)
        {
            gloveRightCollider = glove_R.GetComponent<Collider>();
            if (gloveRightCollider != null)
            {
                Debug.Log($"{gameObject.name}: Znaleziono collider dla prawej rękawicy: {gloveRightCollider.name}, isTrigger: {gloveRightCollider.isTrigger}");

                BodyPartCollision gloveRightCollisionDetector = glove_R.GetComponent<BodyPartCollision>();
                if (gloveRightCollisionDetector == null)
                {
                    gloveRightCollisionDetector = glove_R.gameObject.AddComponent<BodyPartCollision>();
                    Debug.Log($"{gameObject.name}: Dodano BodyPartCollision do prawej rękawicy");
                }
                gloveRightCollisionDetector.Initialize(this, BodyPartType.RightGlove);
            }
            else
            {
                Debug.LogError($"{gameObject.name}: Brak collidera na prawej rękawicy!");
            }
        }

        Debug.Log($"{gameObject.name}: Konfiguracja detekcji kolizji zakończona");
    }

    private void ValidateComponents()
    {
        if (agentRigidbody == null)
            Debug.LogError($"{gameObject.name}: Brak Rigidbody!");
        if (animator == null)
            Debug.LogError($"{gameObject.name}: Brak Animator!");
        if (opponent == null)
            Debug.LogWarning($"{gameObject.name}: Brak przypisanego przeciwnika!");
        if (arenaManager == null)
            Debug.LogWarning($"{gameObject.name}: Brak przypisanego ArenaManager!");

        if (head == null) Debug.LogWarning($"{gameObject.name}: Brak przypisanej głowy!");
        if (body == null) Debug.LogWarning($"{gameObject.name}: Brak przypisanego ciała!");
        if (glove_L == null) Debug.LogWarning($"{gameObject.name}: Brak przypisanej lewej rękawicy!");
        if (glove_R == null) Debug.LogWarning($"{gameObject.name}: Brak przypisanej prawej rękawicy!");
    }

    // Metoda wywoływana przez BodyPartCollision gdy nastąpi kolizja podczas ataku
    public void OnGloveTriggerHit(BodyPartType gloveType, BodyPartType targetType, GameObject target)
    {
        Debug.Log($"🥊 {gameObject.name}: OnGloveTriggerHit wywołane! Glove: {gloveType}, Target: {targetType}, isAttacking: {isCurrentlyAttacking}, currentAttackType: {currentAttackType}");

        if (!isCurrentlyAttacking)
        {
            Debug.Log($"❌ {gameObject.name}: Nie atakuje, ignoruje kolizję");
            return; // Nie atakujemy
        }

        // Sprawdź czy to przeciwnik
        BoxingAgent targetAgent = target.GetComponentInParent<BoxingAgent>();
        if (targetAgent == null || targetAgent == this)
        {
            Debug.Log($"❌ {gameObject.name}: Nieprawidłowy cel lub własne uderzenie. TargetAgent: {(targetAgent != null ? targetAgent.gameObject.name : "null")}");
            return;
        }

        Debug.Log($"🎯 {gameObject.name}: Znaleziono prawidłowy cel: {targetAgent.gameObject.name}");

        // Sprawdź czy to odpowiedni typ ataku
        bool isValidHit = false;

        switch (currentAttackType)
        {
            case 1: // Left_Head_Punch
                if (gloveType == BodyPartType.LeftGlove && targetType == BodyPartType.Head)
                    isValidHit = true;
                break;
            case 2: // Left_Body_Punch
                if (gloveType == BodyPartType.LeftGlove && targetType == BodyPartType.Body)
                    isValidHit = true;
                break;
            case 3: // Right_Head_Punch
                if (gloveType == BodyPartType.RightGlove && targetType == BodyPartType.Head)
                    isValidHit = true;
                break;
            case 4: // Right_Body_Punch
                if (gloveType == BodyPartType.RightGlove && targetType == BodyPartType.Body)
                    isValidHit = true;
                break;
        }

        Debug.Log($"🔍 {gameObject.name}: Sprawdzanie trafienia - AttackType: {currentAttackType}, GloveType: {gloveType}, TargetType: {targetType}, IsValid: {isValidHit}");

        if (isValidHit)
        {
            Debug.Log($"✅ {gameObject.name}: PRAWIDŁOWE TRAFIENIE! {gloveType} -> {targetType}");

            // Sprawdź czy przeciwnik się broni
            if (targetAgent.isDefending && targetAgent.IsCorrectDefense(currentAttackType))
            {
                Debug.Log($"🛡️ {gameObject.name}: Atak obroniony!");
                targetAgent.AddReward(defenseReward);
                targetAgent.RecordSuccessfulDefense();
                AddReward(-0.5f); // Mała kara za zablokowany atak
                RecordMissedAttack();
                SetStunned(0.3f);
                return;
            }

            // Zadaj obrażenia i nagrody - powiadom oba agenty
            Debug.Log($"💥 {gameObject.name}: Wywołuję OnReceiveHit na {targetAgent.gameObject.name}");
            targetAgent.OnReceiveHit(targetType, this);

            Debug.Log($"🏆 {gameObject.name}: Dodaję nagrodę za trafienie: +{hitReward}");
            AddReward(hitReward);
            RecordHit();

            Debug.Log($"📊 {gameObject.name}: Hit recorded! Reward: +{hitReward}, Total episode hits: {episodeHits}");
        }
        else
        {
            Debug.Log($"❌ {gameObject.name}: Nieprawidłowe trafienie - nie pasuje do typu ataku");
        }
    }

    // Metoda wywoływana gdy nasz agent otrzyma uderzenie
    public void OnReceiveHit(BodyPartType hitBodyPart, BoxingAgent attacker)
    {
        Debug.Log($"📍 {gameObject.name}: OnReceiveHit wywołane! HitBodyPart: {hitBodyPart}, Attacker: {attacker.gameObject.name}");
        Debug.Log($"📍 {gameObject.name}: Attacker currentAttackType: {attacker.currentAttackType}, My isDefending: {isDefending}, My lastAction: {lastAction}");

        // Sprawdź czy się bronimy
        if (isDefending && IsCorrectDefense(attacker.currentAttackType))
        {
            Debug.Log($"🛡️ {gameObject.name}: Pomyślnie obronił się!");
            AddReward(defenseReward);
            attacker.AddReward(-0.5f);
            attacker.SetStunned(0.3f);
            RecordSuccessfulDefense();
            return;
        }

        // Nie udało się obronić - otrzymujemy obrażenia
        Debug.Log($"💥 {gameObject.name}: Nie udało się obronić, otrzymuję obrażenia");
        RecordFailedDefense();
        TakeDamage();

        Debug.Log($"📊 {gameObject.name}: Otrzymał obrażenia, HP: {currentHealth}/{maxHealth}");
    }

    private bool IsCorrectDefense(int attackType)
    {
        // Head attacks (1,3) vs Head block (6)
        if ((attackType == 1 || attackType == 3) && lastAction == 6)
            return true;

        // Body attacks (2,4) vs Body block (5)  
        if ((attackType == 2 || attackType == 4) && lastAction == 5)
            return true;

        return false;
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log($"{gameObject.name}: Rozpoczynanie nowego epizodu");

        // Reset health
        currentHealth = maxHealth;

        // Reset states
        isStunned = false;
        isDefending = false;
        isCurrentlyAttacking = false;
        canAct = true;
        stunTimer = 0f;
        actionCooldown = 0f;
        lastAction = 0;
        episodeTimer = 0f;
        currentAttackType = 0;

        // Reset episode statistics
        episodeHits = 0;
        episodeMissedAttacks = 0;
        episodeAttacks = 0;
        episodeDefenseAttempts = 0;
        episodeSuccessfulDefenses = 0;

        // Stop any active attack coroutine
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        // Reset animator (sprawdź czy parametr istnieje)
        if (animator != null)
        {
            // Sprawdź czy parametr istnieje przed jego użyciem
            if (HasAnimatorParameter("FormOfAcction"))
            {
                animator.SetInteger("FormOfAcction", 0);
            }
        }

        // Reset physics
        if (agentRigidbody != null)
        {
            agentRigidbody.linearVelocity = Vector3.zero;
            agentRigidbody.angularVelocity = Vector3.zero;
        }

        // Position through ArenaManager
        if (arenaManager != null)
        {
            arenaManager.PositionAgents();
        }
        else
        {
            RandomizeStartPositionFallback();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Własna pozycja (3)
        sensor.AddObservation(transform.localPosition.x);
        sensor.AddObservation(transform.localPosition.y);
        sensor.AddObservation(transform.localPosition.z);

        // Własna rotacja (3)
        Vector3 eulerRotation = transform.rotation.eulerAngles;
        sensor.AddObservation(eulerRotation.x / 360f);
        sensor.AddObservation(eulerRotation.y / 360f);
        sensor.AddObservation(eulerRotation.z / 360f);

        // Pozycja przeciwnika (3)
        if (opponent != null)
        {
            Vector3 relativePos = opponent.position - transform.position;
            sensor.AddObservation(relativePos.x);
            sensor.AddObservation(relativePos.y);
            sensor.AddObservation(relativePos.z);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Rotacja przeciwnika (3)
        if (opponent != null)
        {
            Vector3 opponentEuler = opponent.rotation.eulerAngles;
            sensor.AddObservation(opponentEuler.x / 360f);
            sensor.AddObservation(opponentEuler.y / 360f);
            sensor.AddObservation(opponentEuler.z / 360f);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Własne HP (1)
        sensor.AddObservation(currentHealth / maxHealth);

        // HP przeciwnika (1)
        if (opponent != null)
        {
            BoxingAgent opponentAgent = opponent.GetComponent<BoxingAgent>();
            if (opponentAgent != null)
            {
                sensor.AddObservation(opponentAgent.currentHealth / opponentAgent.maxHealth);
            }
            else
            {
                sensor.AddObservation(1f);
            }
        }
        else
        {
            sensor.AddObservation(1f);
        }

        // Stany agenta (4) - uproszczone
        sensor.AddObservation(isStunned ? 1f : 0f);
        sensor.AddObservation(isDefending ? 1f : 0f);
        sensor.AddObservation(isCurrentlyAttacking ? 1f : 0f);
        sensor.AddObservation(canAct ? 1f : 0f);

        // Prędkość (3)
        if (agentRigidbody != null)
        {
            sensor.AddObservation(agentRigidbody.linearVelocity.x);
            sensor.AddObservation(agentRigidbody.linearVelocity.y);
            sensor.AddObservation(agentRigidbody.linearVelocity.z);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Kierunek do przeciwnika (3)
        if (opponent != null)
        {
            Vector3 dirToOpponent = (opponent.position - transform.position).normalized;
            sensor.AddObservation(dirToOpponent.x);
            sensor.AddObservation(dirToOpponent.y);
            sensor.AddObservation(dirToOpponent.z);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Czas epizodu (1)
        sensor.AddObservation(episodeTimer / maxEpisodeTime);

        // Dystans do granic areny (4)
        if (arenaManager != null && arenaManager.arenaCenter != null)
        {
            Vector3 pos = transform.position;
            Vector3 center = arenaManager.arenaCenter.position;
            Vector2 arenaSize = arenaManager.arenaSize;

            float distToFront = (center.z + arenaSize.y * 0.5f) - pos.z;
            float distToBack = pos.z - (center.z - arenaSize.y * 0.5f);
            float distToLeft = pos.x - (center.x - arenaSize.x * 0.5f);
            float distToRight = (center.x + arenaSize.x * 0.5f) - pos.x;

            sensor.AddObservation(distToFront / arenaSize.y);
            sensor.AddObservation(distToBack / arenaSize.y);
            sensor.AddObservation(distToLeft / arenaSize.x);
            sensor.AddObservation(distToRight / arenaSize.x);
        }
        else
        {
            sensor.AddObservation(0.5f);
            sensor.AddObservation(0.5f);
            sensor.AddObservation(0.5f);
            sensor.AddObservation(0.5f);
        }

        // Dodatkowe obserwacje (4) - uproszczone
        sensor.AddObservation(stunTimer / stunDuration);
        sensor.AddObservation(lastAction / 6f);

        if (opponent != null)
        {
            Vector3 forwardDir = transform.forward * directionMultiplier;
            Vector3 toOpponent = (opponent.position - transform.position).normalized;
            sensor.AddObservation(Vector3.Dot(forwardDir, toOpponent));
        }
        else
        {
            sensor.AddObservation(0f);
        }

        sensor.AddObservation(Vector3.Distance(transform.position, opponent != null ? opponent.position : transform.position));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        episodeTimer += Time.fixedDeltaTime;

        // Sprawdź czy mamy wystarczająco akcji
        if (actions.ContinuousActions.Length < 3 || actions.DiscreteActions.Length < 1)
        {
            Debug.LogError($"{gameObject.name}: Nieprawidłowa liczba akcji! Continuous: {actions.ContinuousActions.Length}, Discrete: {actions.DiscreteActions.Length}");
            return;
        }

        // Update timers
        if (isStunned)
        {
            stunTimer -= Time.fixedDeltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                canAct = true;
            }
        }

        if (actionCooldown > 0f)
        {
            actionCooldown -= Time.fixedDeltaTime;
            if (actionCooldown <= 0f)
            {
                isDefending = false;
            }
        }

        // Check arena limits
        CheckArenaLimits();

        // Movement
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float rotate = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        HandleMovement(moveX, moveZ, rotate);

        // Combat actions
        int combatAction = actions.DiscreteActions[0];
        HandleCombatAction(combatAction);

        // Check episode end conditions
        CheckEpisodeEndConditions();
    }

    private void HandleMovement(float moveX, float moveZ, float rotate)
    {
        if (isStunned || !canAct) return;

        Vector3 moveDirection = new Vector3(moveX, 0f, moveZ).normalized;

        if (moveDirection.magnitude > 0.1f)
        {
            Vector3 correctedDirection = moveDirection * directionMultiplier;
            Vector3 worldDirection = transform.TransformDirection(correctedDirection);
            Vector3 newPosition = transform.position + worldDirection * moveSpeed * Time.fixedDeltaTime;
            agentRigidbody.MovePosition(newPosition);
        }

        if (Mathf.Abs(rotate) > 0.1f)
        {
            float rotationAmount = rotate * rotationSpeed * Time.fixedDeltaTime;
            Quaternion deltaRotation = Quaternion.Euler(0f, rotationAmount, 0f);
            agentRigidbody.MoveRotation(agentRigidbody.rotation * deltaRotation);
        }
    }

    private void HandleCombatAction(int action)
    {
        if (isStunned || !canAct || actionCooldown > 0f)
        {
            return;
        }

        if (action == 0) return; // Idle

        lastAction = action;

        if (action >= 1 && action <= 4) // Attacks
        {
            Debug.Log($"{gameObject.name}: Rozpoczyna atak typu {action} ({animationTriggers[action]})");
            PerformAttack(action);
        }
        else if (action >= 5 && action <= 6) // Defense
        {
            Debug.Log($"{gameObject.name}: Rozpoczyna obronę typu {action} ({animationTriggers[action]})");
            PerformDefense(action);
        }

        actionCooldown = attackDuration;
    }

    private void PerformAttack(int attackType)
    {
        currentAttackType = attackType;
        RecordAttackAttempt();

        // Trigger animation
        if (animator != null)
        {
            animator.SetTrigger(animationTriggers[attackType]);
        }

        // Start attack coroutine that lasts for the full animation
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }
        attackCoroutine = StartCoroutine(AttackDurationCoroutine());

        Debug.Log($"{gameObject.name}: Atak aktywny przez pełną animację");
    }

    private void PerformDefense(int defenseType)
    {
        isDefending = true;
        RecordDefenseAttempt();

        // Trigger animation
        if (animator != null)
        {
            animator.SetTrigger(animationTriggers[defenseType]);
        }

        StartCoroutine(ReturnToIdleAfterAnimation());
    }

    private IEnumerator AttackDurationCoroutine()
    {
        isCurrentlyAttacking = true;
        bool hitRecorded = false;
        int initialHits = episodeHits;

        Debug.Log($"{gameObject.name}: Attack coroutine started - initialHits: {initialHits}");

        // Wait for animation to start
        yield return null;

        // Wait for animation to complete
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        while (stateInfo.normalizedTime < 1.0f)
        {
            yield return null;
            stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // Check if hit was recorded during attack
            if (episodeHits > initialHits)
            {
                hitRecorded = true;
                Debug.Log($"{gameObject.name}: Hit detected during attack! episodeHits: {episodeHits}");
            }
        }

        // If no hit was recorded during the attack, count it as a miss
        if (!hitRecorded)
        {
            RecordMissedAttack();
            Debug.Log($"{gameObject.name}: No hit recorded during attack - counting as miss");
        }

        // Attack is no longer active
        isCurrentlyAttacking = false;
        currentAttackType = 0;

        // Return to idle (sprawdź czy parametr istnieje)
        if (HasAnimatorParameter("FormOfAcction"))
        {
            animator.SetInteger("FormOfAcction", 0);
        }

        Debug.Log($"{gameObject.name}: Koniec ataku - powrót do Idle. Final episodeHits: {episodeHits}");
    }

    // Pomocnicza metoda do sprawdzania parametrów Animatora
    private bool HasAnimatorParameter(string paramName)
    {
        if (animator == null) return false;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }

    public void TakeDamage()
    {
        float damage = 20f; // Stała wartość obrażeń
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        // Prosta kara za otrzymanie ciosu
        AddReward(hitPenalty);

        // Stun po otrzymaniu obrażeń
        SetStunned(stunDuration);

        Debug.Log($"{gameObject.name}: TakeDamage wywołane! Otrzymał {damage} obrażeń! HP: {currentHealth}/{maxHealth}");
        Debug.Log($"{gameObject.name}: Stack trace: {System.Environment.StackTrace}");

        if (currentHealth <= 0)
        {
            Debug.Log($"{gameObject.name}: Pokonany!");
        }
    }

    public void ApplyKnockback(Vector3 force)
    {
        if (agentRigidbody != null)
        {
            agentRigidbody.AddForce(force, ForceMode.Impulse);
        }
    }

    private void SetStunned(float duration)
    {
        isStunned = true;
        canAct = false;
        stunTimer = duration;
        isCurrentlyAttacking = false;
        currentAttackType = 0;

        // Stop attack coroutine if active
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        Debug.Log($"{gameObject.name}: Stunned na {duration}s");
    }

    private void CheckArenaLimits()
    {
        if (arenaManager != null && !arenaManager.IsInArena(transform.position))
        {
            Debug.Log($"{gameObject.name}: Wyszedł poza arenę!");

            currentHealth -= ArenaDmg;
            currentHealth = Mathf.Max(0, currentHealth);
            AddReward(arenaExitPenalty);

            // Teleport back to arena
            Vector3 center = arenaManager.arenaCenter.position;
            float safeZoneX = (arenaManager.arenaSize.x * 0.5f) - 0.5f;
            float safeZoneZ = (arenaManager.arenaSize.y * 0.5f) - 0.5f;

            Vector3 newPos = new Vector3(
                center.x + Random.Range(-safeZoneX, safeZoneX),
                center.y,
                center.z + Random.Range(-safeZoneZ, safeZoneZ)
            );

            Quaternion newRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            SetRandomPosition(newPos, newRot);
        }
    }

    private void CheckEpisodeEndConditions()
    {
        if (episodeTimer >= maxEpisodeTime)
        {
            HandleEpisodeEnd("REMIS - Czas się skończył");
        }

        if (currentHealth <= 0)
        {
            HandleEpisodeEnd("PRZEGRANA");
        }

        if (opponent != null)
        {
            BoxingAgent opponentAgent = opponent.GetComponent<BoxingAgent>();
            if (opponentAgent != null && opponentAgent.currentHealth <= 0)
            {
                HandleEpisodeEnd("WYGRANA");
            }
        }
    }

    private void HandleEpisodeEnd(string reason)
    {
        totalEpisodes++;

        // Update totals
        totalHits += episodeHits;
        totalMissedAttacks += episodeMissedAttacks;
        totalAttacks += episodeAttacks;
        totalDefenseAttempts += episodeDefenseAttempts;
        totalSuccessfulDefenses += episodeSuccessfulDefenses;

        if (reason.Contains("WYGRANA"))
        {
            wins++;
            AddReward(winReward);
            Debug.Log($"{gameObject.name}: WYGRANA! +{winReward}");
        }
        else if (reason.Contains("PRZEGRANA"))
        {
            losses++;
            AddReward(lossReward);
            Debug.Log($"{gameObject.name}: PRZEGRANA {lossReward}");
        }
        else if (reason.Contains("REMIS"))
        {
            draws++;
            AddReward(drawPenalty);
            Debug.Log($"{gameObject.name}: REMIS {drawPenalty}");
        }

        // Record comprehensive statistics to TensorBoard
        RecordTensorBoardStats();

        EndEpisode();
    }

    private void RecordTensorBoardStats()
    {
        Debug.Log($"{gameObject.name}: Recording TensorBoard stats - Episode: {totalEpisodes}, Hits: {episodeHits}, Attacks: {episodeAttacks}, Misses: {episodeMissedAttacks}");

        if (totalEpisodes > 0)
        {
            // Episode statistics
            Academy.Instance.StatsRecorder.Add("Custom/Episode Length", episodeTimer);
            Academy.Instance.StatsRecorder.Add("Custom/Episode Hits", episodeHits);
            Academy.Instance.StatsRecorder.Add("Custom/Episode Attacks", episodeAttacks);
            Academy.Instance.StatsRecorder.Add("Custom/Episode Missed Attacks", episodeMissedAttacks);

            // Episode rates
            float episodeHitRate = episodeAttacks > 0 ? (float)episodeHits / episodeAttacks * 100f : 0f;
            float episodeDefenseRate = episodeDefenseAttempts > 0 ? (float)episodeSuccessfulDefenses / episodeDefenseAttempts * 100f : 0f;

            Academy.Instance.StatsRecorder.Add("Custom/Episode Hit Rate %", episodeHitRate);
            Academy.Instance.StatsRecorder.Add("Custom/Episode Defense Rate %", episodeDefenseRate);

            Debug.Log($"{gameObject.name}: Episode Hit Rate: {episodeHitRate}%, Defense Rate: {episodeDefenseRate}%");

            // Overall rates
            float winRate = (float)wins / totalEpisodes * 100f;
            float lossRate = (float)losses / totalEpisodes * 100f;
            float drawRate = (float)draws / totalEpisodes * 100f;
            float hitRate = totalAttacks > 0 ? (float)totalHits / totalAttacks * 100f : 0f;
            float missRate = totalAttacks > 0 ? (float)totalMissedAttacks / totalAttacks * 100f : 0f;
            float defenseRate = totalDefenseAttempts > 0 ? (float)totalSuccessfulDefenses / totalDefenseAttempts * 100f : 0f;

            Academy.Instance.StatsRecorder.Add("Custom/Win Rate %", winRate);
            Academy.Instance.StatsRecorder.Add("Custom/Loss Rate %", lossRate);
            Academy.Instance.StatsRecorder.Add("Custom/Draw Rate %", drawRate);
            Academy.Instance.StatsRecorder.Add("Custom/Hit Rate %", hitRate);
            Academy.Instance.StatsRecorder.Add("Custom/Miss Rate %", missRate);
            Academy.Instance.StatsRecorder.Add("Custom/Defense Rate %", defenseRate);
            Academy.Instance.StatsRecorder.Add("Custom/Successful Defense %", defenseRate); // Alias

            Debug.Log($"{gameObject.name}: Overall Hit Rate: {hitRate}%, Miss Rate: {missRate}%");

            // Debug log for verification
            if (showDebugInfo && totalEpisodes % 100 == 0)
            {
                Debug.Log($"{gameObject.name} Stats (Episode {totalEpisodes}): " +
                         $"Win: {winRate:F1}%, Hit: {hitRate:F1}%, Defense: {defenseRate:F1}%");
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: totalEpisodes is 0, not recording stats");
        }
    }

    // Statistics recording methods
    private void RecordHit()
    {
        episodeHits++;
        Debug.Log($"{gameObject.name}: Hit recorded! Episode hits: {episodeHits}");
    }

    private void RecordMissedAttack()
    {
        episodeMissedAttacks++;
        Debug.Log($"{gameObject.name}: Missed attack recorded! Episode misses: {episodeMissedAttacks}");
    }

    private void RecordAttackAttempt()
    {
        episodeAttacks++;
        Debug.Log($"{gameObject.name}: Attack attempt recorded! Episode attacks: {episodeAttacks}");
    }

    private void RecordDefenseAttempt()
    {
        episodeDefenseAttempts++;
        Debug.Log($"{gameObject.name}: Defense attempt recorded! Episode defenses: {episodeDefenseAttempts}");
    }

    private void RecordSuccessfulDefense()
    {
        episodeSuccessfulDefenses++;
        Debug.Log($"{gameObject.name}: Successful defense recorded! Episode successful defenses: {episodeSuccessfulDefenses}");
    }

    private void RecordFailedDefense()
    {
        // Defense attempt was already recorded in PerformDefense, just log the failure
        Debug.Log($"{gameObject.name}: Defense failed!");
    }

    public void SetRandomPosition(Vector3 newPosition, Quaternion newRotation)
    {
        transform.position = newPosition;
        transform.rotation = newRotation;
        if (agentRigidbody != null)
        {
            agentRigidbody.linearVelocity = Vector3.zero;
            agentRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void RandomizeStartPositionFallback()
    {
        float arenaHalfSize = 1.5f;
        float randomX = Random.Range(-arenaHalfSize, arenaHalfSize);
        float randomZ = Random.Range(-arenaHalfSize, arenaHalfSize);
        Vector3 newPosition = new Vector3(randomX, transform.position.y, randomZ);
        transform.position = newPosition;
        float randomRotationY = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, randomRotationY, 0f);
    }

    IEnumerator ReturnToIdleAfterAnimation()
    {
        yield return null;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        while (stateInfo.normalizedTime < 1.0f)
        {
            yield return null;
            stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        }

        animator.SetInteger("FormOfAcction", 0);
        isDefending = false;
        Debug.Log($"{gameObject.name}: Returned to Idle animation");
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;

        if (continuousActions.Length >= 3)
        {
            continuousActions[0] = Input.GetAxisRaw("Horizontal");
            continuousActions[1] = Input.GetAxisRaw("Vertical");
            continuousActions[2] = 0f;

            if (Input.GetKey(KeyCode.Q)) continuousActions[2] = -1f;
            if (Input.GetKey(KeyCode.E)) continuousActions[2] = 1f;
        }

        if (discreteActions.Length >= 1)
        {
            discreteActions[0] = 0;

            for (int i = 1; i <= 6; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    discreteActions[0] = i;
                    Debug.Log($"{gameObject.name}: Klawisz {i} - akcja: {i}");
                    break;
                }
            }
        }
    }

    // Simple getters
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public bool IsStunned() => isStunned;
    public bool IsDefending() => isDefending;
    public bool IsCurrentlyAttacking() => isCurrentlyAttacking;
    public int GetWins() => wins;
    public int GetLosses() => losses;
    public int GetDraws() => draws;
    public float GetWinRate() => totalEpisodes > 0 ? (float)wins / totalEpisodes * 100f : 0f;

    private void OnDrawGizmos()
    {
        if (!showDebugInfo) return;

        // Show HP above head
        Vector3 healthPos = transform.position + Vector3.up * 2.5f;
        float healthPercent = (currentHealth / maxHealth) * 100f;

        Color healthColor = healthPercent > 50f ? Color.green :
                           healthPercent > 25f ? Color.yellow : Color.red;

        GUIStyle style = new GUIStyle();
        style.normal.textColor = healthColor;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;

        string healthText = $"HP: {currentHealth:F0}/{maxHealth:F0}";
        if (totalEpisodes > 0)
        {
            healthText += $"\nW/L/D: {wins}/{losses}/{draws}";
            healthText += $"\nWin: {GetWinRate():F1}%";
        }

        UnityEditor.Handles.Label(healthPos, healthText, style);

        // Show attack state
        if (isCurrentlyAttacking)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }

        // Show defense state
        if (isDefending)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up, Vector3.one);
        }

        // Show if stunned
        if (isStunned)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1.5f, Vector3.one * 0.5f);
        }
    }
}