// CTA.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Splatform;

public class CTA : MonoBehaviour, Interactable, TextReceiver
{
    private void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_character = GetComponent<Character>();
        m_TameableAI = GetComponent<TameableAI>();
        m_CTP = GetComponent<CTP>();

        originalName = m_character.m_name;  

        if (m_nview.IsValid())
        {
            string savedName = m_nview.GetZDO().GetString(ZDOVars.s_tamedName, originalName);
            m_character.m_name = savedName;
            InitializeCustomEffects();
            m_nview.Register<ZDOID, bool, bool>("Command", RPC_Command);
            m_nview.Register<string, string>("SetName", RPC_SetName);
            m_nview.Register("RPC_UnSummon", RPC_UnSummon);
        }

        var config = AnimalConfig.GetConfig(originalName);
        if (config != null)
        {
            m_tamingTime     = config.TamingTime;
            m_fedDuration    = config.FedDuration;
            m_maxLovePoints  = config.RequiredLovePoints;
        }
    }

    public void Unregister()
    {
        if (m_nview != null && m_nview.IsValid())
        {
            m_nview.Unregister("Command");
            m_nview.Unregister("SetName");
            m_nview.Unregister("RPC_UnSummon");
        }
    }

    internal void InitializeCustomEffects()
    {
        var config = AnimalConfig.GetConfig(originalName);
        if (config == null)
        {
            return;
        }

        GameObject boarPetFX = ZNetScene.instance.GetPrefab("fx_boar_pet");
        if (boarPetFX != null)
        {
            ParticleSystem boarPetVFX = boarPetFX.GetComponentInChildren<ParticleSystem>();
            if (boarPetVFX != null)
            {
                this.m_petEffect = new EffectList
                {
                    m_effectPrefabs = new EffectList.EffectData[]
                    {
                        new EffectList.EffectData
                        {
                            m_prefab = boarPetVFX.gameObject,
                            m_enabled = true,
                            m_attach = false,
                            m_follow = false
                        }
                    }
                };
            }
        }

        LoadEffect(config.PetEffectPrefab, ref this.m_petEffect);
        LoadEffect(config.TamedEffectPrefab, ref this.m_tamedEffect);
        LoadEffect(config.SootheEffectPrefab, ref this.m_sootheEffect);
    }

    private void LoadEffect(string effectPrefabName, ref EffectList effectList)
    {
        if (string.IsNullOrEmpty(effectPrefabName))
        {
            return;
        }

        GameObject effectPrefab = ZNetScene.instance.GetPrefab(effectPrefabName);
        if (effectPrefab == null)
        {
            return;
        }

        if (effectList == null)
        {
            effectList = new EffectList
            {
                m_effectPrefabs = new EffectList.EffectData[]
                {
                    new EffectList.EffectData
                    {
                        m_prefab = effectPrefab,
                        m_enabled = true,
                        m_attach = false,
                        m_follow = false
                    }
                }
            };
        }
        else
        {
            EffectList.EffectData[] newEffects = new EffectList.EffectData[effectList.m_effectPrefabs.Length + 1];
            for (int i = 0; i < effectList.m_effectPrefabs.Length; i++)
            {
                newEffects[i] = effectList.m_effectPrefabs[i];
            }
            newEffects[effectList.m_effectPrefabs.Length] = new EffectList.EffectData
            {
                m_prefab = effectPrefab,
                m_enabled = true,
                m_attach = false,
                m_follow = false
            };
            effectList.m_effectPrefabs = newEffects;
        }
    }

    public void Update()
    {
        this.UpdateSummon();
        this.UpdateSavedFollowTarget();
    }

    public string GetHoverText()
    {
        if (!this.m_nview.IsValid())
        {
            return "";
        }

        string text = Localization.instance.Localize(this.m_character.m_name);
        string useKey = ZInput.instance.GetBoundKeyString("Use");
        string commandKey = "G";

        if (this.m_character.IsTamed())
        {
            text += Localization.instance.Localize($" ( $hud_tame, {this.GetStatusString()} )");

            text += Localization.instance.Localize($"\n[<color=yellow><b>{useKey}</b></color>] $hud_pet"
                        + "  [<color=yellow><b>" + commandKey + "</b></color>] Follow");
            text += Localization.instance.Localize($"\n[<color=yellow><b>Shift + {useKey}</b></color>] Rename");

            int currentLovePoints = this.m_nview.GetZDO().GetInt(ZDOVars.s_lovePoints, 0);
            int maxLovePoints = (int)this.m_maxLovePoints;
            text += "\nLove Points: ";
            for (int i = 0; i < currentLovePoints; i++)
            {
                text += "<color=#ff0000>♥</color>";
            }
            for (int i = currentLovePoints; i < maxLovePoints; i++)
            {
                text += "<color=#808080>♥</color>";
            }

            if (this.m_nview.IsValid())
            {
                long lastFeedingTime = this.m_nview.GetZDO().GetLong(ZDOVars.s_tameLastFeeding, 0L);
                long currentTime = ZNet.instance.GetTime().Ticks;
                float fedDuration = this.m_fedDuration;
                double secondsSinceLastFeeding = (currentTime - lastFeedingTime) / TimeSpan.TicksPerSecond;
                float foodPercentage = Mathf.Clamp01((fedDuration - (float)secondsSinceLastFeeding) / fedDuration) * 100f;
                text += $"\nFood Level: {foodPercentage:0}%";
            }

            if (this.m_CTP != null && this.m_CTP.IsPregnant())
            {
                float timeUntilBirth = this.m_CTP.GetTimeUntilBirth();
                if (timeUntilBirth > 0)
                {
                    TimeSpan time = TimeSpan.FromSeconds(timeUntilBirth);
                    text += $"\nPregnant: {time.Minutes:D2}:{time.Seconds:D2}";
                }
            }

            if (ZInput.GetButtonDown("DeerCommand"))
            {
                this.Command(Player.m_localPlayer, true, stay: false);
                return "";
            }
        }
        else
        {
            int tameness = this.GetTameness();
            if (tameness <= 0)
            {
                text += Localization.instance.Localize($" ( $hud_wild, {this.GetStatusString()} )");
            }
            else
            {
                text += Localization.instance.Localize($" ( Taming Status: {tameness}%, {this.GetStatusString()} )");
            }
        }

        return text;
    }

    public string GetStatusString()
    {
        if (this.m_TameableAI.IsAlerted())
        {
            return "$hud_tamefrightened";
        }
        if (this.IsHungry())
        {
            return "$hud_tamehungry";
        }
        if (this.m_character.IsTamed())
        {
            return "$hud_tamehappy";
        }
        return "$hud_tameinprogress";
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (!this.m_nview.IsValid())
        {
            return false;
        }
        if (hold)
        {
            return false;
        }
        if (!this.m_character.IsTamed())
        {
            return false;
        }

        if (alt)
        {
            this.SetName();
            return true;
        }

        if (ZInput.GetButtonDown("Use"))
        {
            if (Time.time - this.m_lastPetTime > 1f)
            {
                this.m_lastPetTime = Time.time;

                if (this.m_petEffect != null)
                {
                    this.m_petEffect.Create(base.transform.position, base.transform.rotation, null, 1f, -1);
                }

                int currentLovePoints = this.m_nview.GetZDO().GetInt(ZDOVars.s_lovePoints, 0);
                if (currentLovePoints < this.m_maxLovePoints)
                {
                    currentLovePoints++;
                    this.m_nview.GetZDO().Set(ZDOVars.s_lovePoints, currentLovePoints);
                }

                user.Message(MessageHud.MessageType.Center, this.m_character.GetHoverName() + " $hud_tamelove", 0, null);
                return true;
            }
        }

        if (ZInput.GetButtonDown("DeerCommand"))
        {
            this.Command(user, true);
            return true;
        }

        return false;
    }

    public string GetHoverName()
    {
        if (!this.m_character.IsTamed())
        {
            return Localization.instance.Localize(this.m_character.m_name);
        }
        string text = this.GetText().RemoveRichTextTags();
        if (text.Length > 0)
        {
            return text;
        }
        return Localization.instance.Localize(this.m_character.m_name);
    }

    private void SetName()
    {
        if (!this.m_character.IsTamed())
        {
            return;
        }
        TextInput.instance.RequestText(this, "$hud_rename", 10);
    }

    public string GetText()
	{
		if (!this.m_nview.IsValid())
		{
			return "";
		}
		return CensorShittyWords.FilterUGC(this.m_nview.GetZDO().GetString(ZDOVars.s_tamedName, ""), UGCType.Text, new PlatformUserID(this.m_nview.GetZDO().GetString(ZDOVars.s_tamedNameAuthor, "")), 0L);
	}

    public void SetText(string text)
	{
		if (!this.m_nview.IsValid())
		{
			return;
		}
		this.m_nview.InvokeRPC("SetName", new object[]
		{
			text,
			PlatformManager.DistributionPlatform.LocalUser.PlatformUserID.ToString()
		});
	}

    private void RPC_SetName(long sender, string name, string authorId)
    {
        if (!this.m_nview.IsValid() || !this.m_nview.IsOwner())
        {
            return;
        }
        if (!this.m_character.IsTamed())
        {
            return;
        }

        this.m_nview.GetZDO().Set(ZDOVars.s_tamedName, name);
        this.m_character.m_name = name;

    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        if (!this.m_nview.IsValid())
        {
            return false;
        }

        if (item != null && item.m_shared != null)
        {
            // Implement any additional item interactions here if necessary.
        }

        return false;
    }

    internal void ResetFeedingTimer()
    {
        this.m_nview.GetZDO().Set(ZDOVars.s_tameLastFeeding, ZNet.instance.GetTime().Ticks);
    }

    public void Tame()
    {
        Game.instance.IncrementPlayerStat(PlayerStatType.CreatureTamed, 1f);
        if (!this.m_nview.IsValid() || !this.m_nview.IsOwner())
        {
            return;
        }
        if (this.m_character.IsTamed())
        {
            return;
        }
        this.m_TameableAI.MakeTame();
        this.m_tamedEffect.Create(base.transform.position, base.transform.rotation, null, 1f, -1);
        Player closestPlayer = Player.GetClosestPlayer(base.transform.position, 30f);
        if (closestPlayer)
        {
            closestPlayer.Message(MessageHud.MessageType.Center, this.m_character.m_name + " $hud_tamedone", 0, null);
        }
    }

    public void Command(Humanoid user, bool message = true, bool stay = false)
    {
        this.m_nview.InvokeRPC("Command", new object[]
        {
            user.GetZDOID(),
            message,
            stay
        });

        if (this.m_nview.IsOwner())
        {
            if (!stay && IsFollowingPlayer(user))
            {
                EnablePortalFollower();
            }
            else
            {
                DisablePortalFollower();
            }
        }
    }

    private void EnablePortalFollower()
    {
        if (gameObject.GetComponent<PortalFollower>() == null)
        {
            gameObject.AddComponent<PortalFollower>();
        }
    }

    private void DisablePortalFollower()
    {
        PortalFollower portalFollower = gameObject.GetComponent<PortalFollower>();
        if (portalFollower != null)
        {
            Destroy(portalFollower);
        }
    }
    
    private bool IsFollowingPlayer(Humanoid user)
    {
        return m_character.IsTamed() && m_TameableAI.GetFollowTarget() == user.gameObject;
    }
    
    private void RPC_Command(long sender, ZDOID characterID, bool message, bool stay)
    {
        Player player = this.GetPlayer(characterID);
        if (player == null)
        {
            return;
        }
        else if (this.m_TameableAI.GetFollowTarget())
        {
            this.m_TameableAI.SetFollowTarget(null);
            this.m_TameableAI.SetPatrolPoint();

            if (this.m_nview.IsOwner())
            {
                this.m_nview.GetZDO().Set(ZDOVars.s_follow, "");
            }

            player.Message(MessageHud.MessageType.Center, this.m_character.GetHoverName() + " $hud_tamestay", 0, null);
        }
        else
        {
            this.m_TameableAI.ResetPatrolPoint();
            this.m_TameableAI.SetFollowTarget(player.gameObject);

            if (this.m_nview.IsOwner())
            {
                this.m_nview.GetZDO().Set(ZDOVars.s_follow, player.GetPlayerName());
            }

            player.Message(MessageHud.MessageType.Center, this.m_character.GetHoverName() + " $hud_tamefollow", 0, null);
        }
    }

    private void UpdateSavedFollowTarget()
    {
        if (this.m_TameableAI.GetFollowTarget() != null || !this.m_nview.IsOwner())
        {
            return;
        }
        string followPlayerName = this.m_nview.GetZDO().GetString(ZDOVars.s_follow, "");
        if (string.IsNullOrEmpty(followPlayerName))
        {
            return;
        }
        foreach (Player player in Player.GetAllPlayers())
        {
            if (player.GetPlayerName() == followPlayerName)
            {
                this.Command(player, false);
                return;
            }
        }
        if (this.m_unsummonOnOwnerLogoutSeconds > 0f)
        {
            this.m_unsummonTime += Time.fixedDeltaTime;
            if (this.m_unsummonTime > this.m_unsummonOnOwnerLogoutSeconds)
            {
                this.UnSummon();
            }
        }
    }

    public bool IsHungry()
    {
        if (this.m_nview == null) return false;

        ZDO zdo = this.m_nview.GetZDO();
        DateTime lastFedTime = new DateTime(zdo.GetLong(ZDOVars.s_tameLastFeeding, 0L));

        return (ZNet.instance.GetTime() - lastFedTime).TotalSeconds > this.m_fedDuration;
    }

    private void RPC_UnSummon(long sender)
    {
        this.m_unSummonEffect.Create(base.gameObject.transform.position, base.gameObject.transform.rotation, null, 1f, -1);
        if (!this.m_nview.IsValid() || !this.m_nview.IsOwner())
        {
            return;
        }
        ZNetScene.instance.Destroy(base.gameObject);
    }
    
    private void OnDestroy()
    {
        Unregister();
    }
    
    private void UpdateSummon()
    {
        if (!this.m_nview.IsValid() || !this.m_nview.IsOwner())
        {
            return;
        }
        if (this.m_unsummonDistance > 0f && this.m_TameableAI)
        {
            GameObject followTarget = this.m_TameableAI.GetFollowTarget();
            if (followTarget && Vector3.Distance(followTarget.transform.position, base.gameObject.transform.position) > this.m_unsummonDistance)
            {
                this.UnSummon();
            }
        }
    }

    private void UnSummon()
    {
        if (!this.m_nview.IsValid())
        {
            return;
        }
        this.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_UnSummon", Array.Empty<object>());
    }
    
    private Player GetPlayer(ZDOID characterID)
    {
        GameObject gameObject = ZNetScene.instance.FindInstance(characterID);
        if (gameObject)
        {
            return gameObject.GetComponent<Player>();
        }
        return null;
    }

    public void DecreaseRemainingTime(float time)
    {
        if (!this.m_nview.IsValid())
        {
            return;
        }
        float num = this.GetRemainingTime();
        num -= time;
        if (num < 0f)
        {
            num = 0f;
        }
        this.m_nview.GetZDO().Set(ZDOVars.s_tameTimeLeft, num);
    }

    public float GetRemainingTime()
    {
        if (!this.m_nview.IsValid())
        {
            return 0f;
        }
        return this.m_nview.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, this.m_tamingTime);
    }

    public int GetTameness()
    {
        float remainingTime = this.GetRemainingTime();
        return (int)((1f - Mathf.Clamp01(remainingTime / this.m_tamingTime)) * 100f);
    }

    public void OnConsumedItem(ItemDrop item)
    {
        if (this.IsHungry())
        {
            this.ResetFeedingTimer();
        }

        if (m_CTP != null && m_CTP.IsPregnant())
        {
            return;
        }

        if (m_nview.IsValid() && m_character.IsTamed())
        {
            int currentLovePoints = m_nview.GetZDO().GetInt(ZDOVars.s_lovePoints, 0);
            if (currentLovePoints < m_maxLovePoints)
            {
                currentLovePoints++;
                m_nview.GetZDO().Set(ZDOVars.s_lovePoints, currentLovePoints);
                if (currentLovePoints >= m_maxLovePoints && m_CTP != null)
                {
                    m_CTP.Procreate();
                }
            }
        }
        this.m_sootheEffect.Create(this.m_character.GetCenterPoint(), Quaternion.identity, null, 1f, -1);
    }

    private const float m_playerMaxDistance = 15f;
    private const float m_tameDeltaTime = 3f;
    public float m_fedDuration = 30f;
    public float m_tamingTime = 1800f;
    public EffectList m_tamedEffect = new EffectList();
    public EffectList m_sootheEffect = new EffectList();
    public EffectList m_petEffect = new EffectList();
    public float m_unsummonDistance;
    public float m_unsummonOnOwnerLogoutSeconds;
    public EffectList m_unSummonEffect = new EffectList();
    public float m_lovePoints = 0f;
    public float m_maxLovePoints = 5f;
    internal Character m_character;
    private CTP m_CTP;
    internal TameableAI m_TameableAI;
    internal ZNetView m_nview;
    private float m_lastPetTime;
    private float m_unsummonTime;
    internal string originalName;

}