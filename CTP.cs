
// CTP.cs
using System;
using UnityEngine;
using Beyondthepen;
using System.Collections.Generic;

public class CTP : MonoBehaviour
{
    private static class ZDOVars
    {
        public const string s_pregnant = "pregnant";
        public const string s_lovePoints = "lovePoints";
    }

    private void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_baseAI = GetComponent<BaseAI>();
        m_character = GetComponent<Character>();
        m_tameable = GetComponent<CTA>();
        
        var config = AnimalConfig.GetConfig(m_character.m_name);
        if (config != null)
        {
            m_maxCreatures = config.MaxCreatures;
            m_partnerCheckRange = config.PartnerCheckRange;
            m_pregnancyChance = config.PregnancyChance;
            m_pregnancyDuration = config.PregnancyDuration;
            m_spawnOffset = config.SpawnOffset;
            m_requiredLovePoints = config.RequiredLovePoints;
            m_minOffspringLevel = config.MinOffspringLevel;
        }

        InvokeRepeating(nameof(Procreate), UnityEngine.Random.Range(m_updateInterval, m_updateInterval + m_updateInterval * 0.5f), m_updateInterval);
    }

    public void Procreate()
    {
        if (IsPregnant())
        {
            if (IsDue())
            {
                ResetPregnancy();
                Vector3 spawnPosition = transform.position - transform.forward * m_spawnOffset;
                GameObject newOffspring = Instantiate(gameObject, spawnPosition, Quaternion.LookRotation(-transform.forward, Vector3.up));

                if (newOffspring == null)
                {
                    return;
                }

                Character offspringCharacter = newOffspring.GetComponent<Character>();
                if (offspringCharacter != null)
                {
                    offspringCharacter.SetTamed(m_character.IsTamed());
                    offspringCharacter.SetLevel(Mathf.Max(m_minOffspringLevel, m_character.GetLevel()));
                }

                CTA parentCTA = m_character.GetComponent<CTA>();
                CTA offspringCTA = newOffspring.GetComponent<CTA>();
                if (parentCTA != null && offspringCTA != null)
                {
                    offspringCTA.originalName = parentCTA.originalName;
                }

                m_birthEffects.Create(newOffspring.transform.position, Quaternion.identity, null, 1f, -1);
            }
            return;
        }

        if (!ReadyForProcreation())
        {
            return;
        }

        int lovePoints = m_nview.GetZDO().GetInt(ZDOVars.s_lovePoints, 0);
        if (lovePoints < m_requiredLovePoints)
        {
            return;
        }

        if (m_tameable.IsHungry())
        {
            return;
        }

        int offspringCount = GetTamedDeerCount(transform.position, m_totalCheckRange);
        if (offspringCount >= m_maxCreatures)
        {
            return;
        }

        List<CTA> partners = FindNearbyPartners();
        if (partners.Count == 0)
        {
            return;
        }

        CTA partner = partners[0];
        int partnerLovePoints = partner.m_nview.GetZDO().GetInt(ZDOVars.s_lovePoints, 0);
        if (partnerLovePoints < m_requiredLovePoints)
        {
            return;
        }

        if (UnityEngine.Random.value <= m_pregnancyChance)
        {
            long nowTicks = ZNet.instance.GetTime().Ticks;
            m_nview.GetZDO().Set(ZDOVars.s_pregnant, nowTicks);
            m_nview.GetZDO().Set(ZDOVars.s_lovePoints, 0);
            partner.m_nview.GetZDO().Set(ZDOVars.s_lovePoints, 0);
            m_loveEffects.Create(transform.position, Quaternion.identity, null, 1f, -1);
        }
    }

    private List<CTA> FindNearbyPartners()
    {
        List<CTA> nearbyPartners = new List<CTA>();

        foreach (BaseAI baseAI in BaseAI.BaseAIInstances)
        {
            if (baseAI != null && Vector3.Distance(baseAI.transform.position, transform.position) <= m_partnerCheckRange)
            {
                CTA tameable = baseAI.GetComponent<CTA>();
                if (tameable != null && tameable.m_character.IsTamed() && tameable.m_character.m_name == m_character.m_name)
                {
                    nearbyPartners.Add(tameable);
                }
            }
        }

        return nearbyPartners;
    }

    public bool ReadyForProcreation()
    {
        return m_character.IsTamed() && !IsPregnant() && !m_tameable.IsHungry();
    }

    private void ResetPregnancy()
    {
        m_nview.GetZDO().Set(ZDOVars.s_pregnant, 0L);
    }

    internal bool IsDue()
    {
        long pregnantTimeTicks = m_nview.GetZDO().GetLong(ZDOVars.s_pregnant, 0L);
        if (pregnantTimeTicks == 0L)
        {
            return false;
        }

        DateTime pregnancyStart = new DateTime(pregnantTimeTicks);
        TimeSpan pregnancyDuration = TimeSpan.FromSeconds(m_pregnancyDuration);
        DateTime currentTime = ZNet.instance.GetTime();
        return (currentTime - pregnancyStart) > pregnancyDuration;
    }

    internal bool IsPregnant()
    {
        return m_nview.IsValid() && m_nview.GetZDO().GetLong(ZDOVars.s_pregnant, 0L) != 0L;
    }

    private int GetTamedDeerCount(Vector3 position, float range)
    {
        int count = 0;
        foreach (BaseAI baseAI in BaseAI.BaseAIInstances)
        {
            if (baseAI != null && Vector3.Distance(position, baseAI.transform.position) <= range)
            {
                Character character = baseAI.GetComponent<Character>();
                if (character != null && character.name.Contains("Deer") && character.IsTamed())
                {
                    count++;
                }
            }
        }
        return count;
    }

    internal float GetTimeUntilBirth()
    {
        if (!IsPregnant())
        {
            return 0f;
        }

        long pregnantTimeTicks = m_nview.GetZDO().GetLong(ZDOVars.s_pregnant, 0L);
        DateTime pregnancyStart = new DateTime(pregnantTimeTicks);
        TimeSpan pregnancyDuration = TimeSpan.FromSeconds(m_pregnancyDuration);
        DateTime currentTime = ZNet.instance.GetTime();

        float timeRemaining = (float)(pregnancyDuration - (currentTime - pregnancyStart)).TotalSeconds;
        return Mathf.Max(timeRemaining, 0f);
    }

    public float m_updateInterval = 10f;
    public float m_totalCheckRange = 10f;
    public int m_maxCreatures = 4;
    public float m_partnerCheckRange = 3f;
    public float m_pregnancyChance = 1f;
    public float m_pregnancyDuration = 10f;
    public int m_requiredLovePoints = 5;
    public float m_spawnOffset = 2f;
    public EffectList m_birthEffects = new EffectList();
    public EffectList m_loveEffects = new EffectList();
    internal int m_minOffspringLevel = 1;
    private ZNetView m_nview;
    private BaseAI m_baseAI;
    internal Character m_character;
    private CTA m_tameable;
}