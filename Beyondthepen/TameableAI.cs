// TameableAI //
using System;
using System.Collections.Generic;
using UnityEngine;

public class TameableAI : BaseAI
{
    protected override void Awake()
    {
        base.Awake();
        this.m_tamable = GetComponent<CTA>();
        this.m_procreation = GetComponent<CTP>();
        var config = AnimalConfig.GetConfig(m_character.m_name);
		isAfraidOfPlayer = true;
        if (config != null)
        {
            this.m_despawnInDay = this.m_nview.GetZDO().GetBool(ZDOVars.s_despawnInDay, this.m_despawnInDay);
            this.m_eventCreature = this.m_nview.GetZDO().GetBool(ZDOVars.s_eventCreature, this.m_eventCreature);
            if (this.m_tamable != null)
            {
                this.m_tamable.m_tamingTime = config.TamingTime;
                this.m_tamable.m_fedDuration = config.FedDuration;
                this.m_tamable.m_maxLovePoints = config.RequiredLovePoints;
            }
            if (this.m_procreation != null)
            {
                this.m_procreation.m_pregnancyDuration = config.PregnancyDuration;
                this.m_procreation.m_maxCreatures = config.MaxCreatures;
                this.m_procreation.m_partnerCheckRange = config.PartnerCheckRange;
                this.m_procreation.m_pregnancyChance = config.PregnancyChance;
                this.m_procreation.m_spawnOffset = config.SpawnOffset;
                this.m_procreation.m_requiredLovePoints = config.RequiredLovePoints;
                this.m_procreation.m_minOffspringLevel = config.MinOffspringLevel;
                if (!this.m_character.IsTamed())
                {
                    this.m_procreation.enabled = false;
                }
            }
            this.m_consumeItems = new List<ItemDrop>();
            foreach (var foodItemName in config.FoodItems)
            {
                ItemDrop foodItem = FindItemByName(foodItemName);
                if (foodItem != null)
                {
                    this.m_consumeItems.Add(foodItem);
                }
            }
        }
    }

    private ItemDrop FindItemByName(string itemName)
    {
        if (ObjectDB.instance == null)
        {
            return null;
        }
        GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(itemName);
        if (itemPrefab != null)
        {
            ItemDrop itemDrop = itemPrefab.GetComponent<ItemDrop>();
            if (itemDrop != null)
            {
                return itemDrop;
            }
        }
        return null;
    }

    private void Start()
    {
        if (this.m_nview && this.m_nview.IsValid() && this.m_nview.IsOwner())
        {
            Humanoid humanoid = this.m_character as Humanoid;
            if (humanoid)
            {
                humanoid.EquipBestWeapon(null, null, null, null);
            }
        }
    }

    public void MakeTame()
    {
        this.m_character.SetTamed(true);
        this.SetAlerted(false);
        this.m_targetCreature = null;
        if (this.m_procreation != null)
        {
            this.m_procreation.enabled = true;
        }
    }

	protected override void SetAlerted(bool alert)
    {
        if (alert && this.m_preventAlert)
        {
            alert = false;
        }
        else if (alert)
        {
            this.m_timeSinceSensedTargetCreature = 0f;
        }
        base.SetAlerted(alert);
    }

    public GameObject GetFollowTarget()
    {
        return this.m_follow;
    }

    public void SetFollowTarget(GameObject go)
    {
        this.m_follow = go;
    }

    public override bool UpdateAI(float dt)
	{
		if (!base.UpdateAI(dt))
		{
			return false;
		}

		player = Player.m_localPlayer;
		if (player == null)
		{
			return false; // Exit if player reference is null
		}

		// Check if the player is holding blueberries, but only relevant to wild animals
		if (!this.m_character.IsTamed())
		{
			CheckPlayerBlueberryStatus();
		}

		// Only initiate fleeing if the deer is both wild and hungry (not in taming process or tamed)
		if (isAfraidOfPlayer && !playerHasBlueberries && Vector3.Distance(transform.position, player.transform.position) < alertDistance)
		{
			if (this.m_tamable != null && !this.m_character.IsTamed() && this.m_tamable.GetTameness() == 0)
			{
				FleeFromPlayer(dt);
				return true;
			}
		}

		// Handle time-based reset of alert state
		if (IsAlerted())
		{
			m_inDangerTimer += dt;
			if (m_inDangerTimer > m_timeToSafe)
			{
				SetAlerted(false);
				m_inDangerTimer = 0f;
			}
		}

		// Continue taming if either blueberries are present, or taming has already started
		if (playerHasBlueberries || tamingStarted || this.m_character.IsTamed())
		{
			if (this.m_tamable != null && !this.m_character.IsTamed())
			{
				tamingStarted = true; // Set tamingStarted to true as taming has begun
				UpdateTaming(dt);
			}

			// Hunger check and feeding should proceed even if player lacks blueberries once tamed
			if (this.m_tamable != null && this.m_tamable.IsHungry())
			{
				UpdateConsumeItem(this.m_character, dt);
			}
		}

		// Normal tamed behaviors (procreation, idle routines) should proceed independently
		if (HandleFollowRoutine(dt)) return true;
		if (HandleIdleRoutine(dt)) return true;

		return true;
	}

	public void SetPlayerHasBlueberries(bool hasBlueberries)
    {
        playerHasBlueberries = hasBlueberries;
    }

    private void CheckPlayerBlueberryStatus()
    {
        playerHasBlueberries = HasTamingItem(player);
    }

	private void FleeFromPlayer(float dt)
	{
		this.SetAlerted(true); // Add this line to initiate alert state and intensify fleeing

		Vector3 fleeDirection = (transform.position - player.transform.position).normalized * alertDistance;
		Vector3 fleePosition = transform.position + fleeDirection;

		MoveTo(dt, fleePosition, 0f, true); // Move to a safe distance
	}

    private bool HasTamingItem(Player player)
	{
		Inventory inventory = player.GetInventory();
		foreach (ItemDrop.ItemData item in inventory.GetAllItems())
		{
			if (item.m_shared.m_name == "$item_blueberries" && item.m_stack > 0)
			{
				return true;
			}
		}
		return false;
	}

    private bool HandleIdleRoutine(float dt)
    {
        if (this.m_tamable != null && this.m_character.IsTamed() && this.m_follow == null)
        {
            UpdateIdleBehavior(dt);
            return true;
        }
        return false;
    }

    private bool HandleFollowRoutine(float dt)
    {
        if (this.m_follow != null)
        {
            FollowTarget(dt);
            return true;
        }
        return false;
    }

    private void UpdateTaming(float dt)
    {
        if (this.m_tamable == null || base.IsAlerted() || this.m_tamable.IsHungry())
        {
            return;
        }
        this.m_tamable.DecreaseRemainingTime(3f * dt);
        this.m_tamable.m_sootheEffect.Create(this.m_character.transform.position, this.m_character.transform.rotation, null, 1f, -1);
        if (this.m_tamable.GetRemainingTime() <= 0f)
        {
            this.m_tamable.Tame();
            this.m_preventAlert = false;
            this.m_preventFlee = false;
        }
    }

    private bool UpdateConsumeItem(Character character, float dt)
	{
		if (m_consumeItems == null || m_consumeItems.Count == 0)
		{
			return false;
		}

		m_consumeSearchTimer += dt;
		if (m_consumeSearchTimer > m_consumeSearchInterval)
		{
			m_consumeSearchTimer = 0f;
			
			// Find the closest food item within range and path-check it
			m_consumeTarget = FindClosestConsumableItem(m_consumeSearchRange);
			if (m_consumeTarget == null)
			{
				return false;
			}
		}

		// Check if the animal can move to and face the item to consume
		if (m_consumeTarget != null && base.MoveTo(dt, m_consumeTarget.transform.position, m_consumeRange, false))
		{
			base.LookAt(m_consumeTarget.transform.position);
			if (base.IsLookingAt(m_consumeTarget.transform.position, 20f, false))
			{
				if (m_consumeTarget.RemoveOne())
				{
					m_animator.SetTrigger("consume");
					m_tamable?.OnConsumedItem(m_consumeTarget);
					m_consumeTarget = null;  // Reset target after consumption
				}
			}
		}
		return true;
	}

    public void SetPreventAlertAndFlee(bool prevent)
    {
        this.m_preventAlert = prevent;
        this.m_preventFlee = prevent;
    }

    private ItemDrop FindClosestConsumableItem(float maxRange)
	{
		if (TameableAI.m_itemMask == 0)
		{
			TameableAI.m_itemMask = LayerMask.GetMask("item");
		}

		Collider[] itemsInRange = Physics.OverlapSphere(transform.position, maxRange, m_itemMask);
		ItemDrop closestItem = null;
		float closestDistance = float.MaxValue;

		foreach (var collider in itemsInRange)
		{
			if (collider.attachedRigidbody)
			{
				var itemDrop = collider.attachedRigidbody.GetComponent<ItemDrop>();
				if (itemDrop != null && CanConsume(itemDrop.m_itemData) && base.HavePath(itemDrop.transform.position))
				{
					float distance = Vector3.Distance(transform.position, itemDrop.transform.position);
					if (distance < closestDistance)
					{
						closestItem = itemDrop;
						closestDistance = distance;
					}
				}
			}
		}

		return closestItem;
	}

    private bool CanConsume(ItemDrop.ItemData item)
    {
        using (List<ItemDrop>.Enumerator enumerator = this.m_consumeItems.GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.m_itemData.m_shared.m_name == item.m_shared.m_name)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void SetDespawnInDay(bool despawn)
    {
        this.m_despawnInDay = despawn;
        this.m_nview.GetZDO().Set(ZDOVars.s_despawnInDay, despawn);
    }

    public bool DespawnInDay()
    {
        if (Time.time - this.m_lastDespawnInDayCheck > 4f)
        {
            this.m_lastDespawnInDayCheck = Time.time;
            this.m_despawnInDay = this.m_nview.GetZDO().GetBool(ZDOVars.s_despawnInDay, this.m_despawnInDay);
        }
        return this.m_despawnInDay;
    }

    private void UpdateIdleBehavior(float dt)
    {
        m_idleStateTimer -= dt;

        if (m_idleStateTimer <= 0)
        {
            m_currentIdleState = (IdleState)UnityEngine.Random.Range(0, 3); // Randomly choose new idle state

            switch (m_currentIdleState)
            {
                case IdleState.Patrol:
                    SetPatrolTarget();
                    m_idleStateTimer = UnityEngine.Random.Range(10f, 20f);
                    break;
                case IdleState.Wander:
                    SetWanderTarget();
                    m_idleStateTimer = UnityEngine.Random.Range(5f, 10f);
                    break;
                case IdleState.Sleep:
                    SetSleeping(true);
                    m_idleStateTimer = UnityEngine.Random.Range(20f, 40f);
                    break;
            }
        }

        switch (m_currentIdleState)
        {
            case IdleState.Patrol:
                Patrol(dt);
                break;
            case IdleState.Wander:
                Wander(dt);
                break;
            case IdleState.Sleep:
                Sleep();
                break;
        }
    }

    private void SetPatrolTarget()
    {
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * 10f;
        randomDirection.y = 0;
        m_idleTargetPosition = m_character.transform.position + randomDirection;
    }

    private void Patrol(float dt)
    {
        if (Vector3.Distance(m_character.transform.position, m_idleTargetPosition) > 1f)
        {
            base.MoveTo(dt, m_idleTargetPosition, 0f, false);
        }
        else
        {
            m_idleStateTimer = 0;
        }
    }

    private void SetWanderTarget()
    {
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * 5f;
        randomDirection.y = 0;
        m_idleTargetPosition = m_character.transform.position + randomDirection;
    }

    private void Wander(float dt)
    {
        if (Vector3.Distance(m_character.transform.position, m_idleTargetPosition) > 1f)
        {
            base.MoveTo(dt, m_idleTargetPosition, 0f, false);
        }
        else
        {
            m_idleStateTimer = 0;
        }
    }

    private void Sleep()
    {
        if (!m_isSleeping)
        {
            m_animator.SetBool("sleeping", true);
            m_isSleeping = true;
        }
    }

    private void WakeUp()
    {
        m_animator.SetBool("sleeping", false);
        m_isSleeping = false;
    }

    public void SetEventCreature(bool despawn)
    {
        this.m_eventCreature = despawn;
        this.m_nview.GetZDO().Set(ZDOVars.s_eventCreature, despawn);
    }

    public bool IsEventCreature()
    {
        if (Time.time - this.m_lastEventCreatureCheck > 4f)
        {
            this.m_lastEventCreatureCheck = Time.time;
            this.m_eventCreature = this.m_nview.GetZDO().GetBool(ZDOVars.s_eventCreature, this.m_eventCreature);
        }
        return this.m_eventCreature;
    }

    private void FollowTarget(float dt)
    {
        if (this.m_follow != null)
        {
            Vector3 followPosition = this.m_follow.transform.position;
            float distanceToTarget = Vector3.Distance(base.transform.position, followPosition);

            float sprintThreshold = 10f;
            float walkThreshold = 3f;
            bool shouldSprint = distanceToTarget > sprintThreshold;

            if (distanceToTarget < walkThreshold)
            {
                this.StopMoving();
                return;
            }

            this.MoveTo(dt, followPosition, 0f, shouldSprint);
        }
    }

    private void SetSleeping(bool isSleeping)
    {
        if (isSleeping && !m_isSleeping)
        {
            m_animator.SetBool("sleeping", true);
            m_isSleeping = true;
        }
        else if (!isSleeping && m_isSleeping)
        {
            m_animator.SetBool("sleeping", false);
            m_isSleeping = false;
        }
    }

	private enum IdleState
    {
        Patrol,
        Wander,
        Sleep
    }

	private float m_lastDespawnInDayCheck = -9999f;
    private float m_lastEventCreatureCheck = -9999f;
    public Action<ItemDrop> m_onConsumedItem;
    public float m_alertRange = 9999f; 
    public bool m_fleeIfHurtWhenTargetCantBeReached = true; 
    public float m_fleeUnreachableSinceAttacking = 30f; 
    public float m_fleeUnreachableSinceHurt = 20f; 
    public bool m_fleeIfNotAlerted; 
    public float m_fleeIfLowHealth; 
    public float m_fleeTimeSinceHurt = 20f; 
    public bool m_fleeInLava = true; 
    public bool m_circulateWhileCharging; 
    public bool m_circulateWhileChargingFlying; 
    public bool m_enableHuntPlayer; 
    public bool m_attackPlayerObjects = false; 
    public int m_privateAreaTriggerTreshold = 4; 
    public float m_interceptTimeMax; 
    public float m_interceptTimeMin; 
    public float m_maxChaseDistance; 
    public float m_minAttackInterval; 
    public float m_circleTargetInterval; 
    public float m_circleTargetDuration = 5f; 
    public float m_circleTargetDistance = 10f; 
    public bool m_sleeping;
    public float m_wakeupRange = 5f;
    public bool m_noiseWakeup;
    public float m_maxNoiseWakeupRange = 50f;
    public EffectList m_wakeupEffects = new EffectList();
    public float m_wakeUpDelayMin;
    public float m_wakeUpDelayMax;
    public bool m_avoidLand;
    public List<ItemDrop> m_consumeItems;
    public float m_consumeRange = 1.5f;
    public float m_consumeSearchRange = 20f;
    public float m_consumeSearchInterval = 1f;
    private ItemDrop m_consumeTarget;
    private float m_consumeSearchTimer;
    private static int m_itemMask = 0;
    private bool m_despawnInDay;
    private bool m_eventCreature;
    private Character m_targetCreature; 
    private Vector3 m_lastKnownTargetPos = Vector3.zero; 
    private float m_timeSinceSensedTargetCreature; 
    private float m_unableToAttackTargetTimer; 
    private GameObject m_follow;
    private static readonly int s_sleeping = ZSyncAnimation.GetHash("sleeping");
    public new CTA m_tamable;
    private CTP m_procreation;
    private bool m_preventAlert = false;
    private bool m_preventFlee = false;
    private IdleState m_currentIdleState;
    private float m_idleStateTimer;
    private Vector3 m_idleTargetPosition;
    private bool m_isSleeping;
	private Player player;
    private bool playerHasBlueberries;
    private float alertDistance = 20f;
    private bool isAfraidOfPlayer;
	private float m_inDangerTimer = 0f;
	public float m_timeToSafe = 4f;
	private bool tamingStarted = false;

}