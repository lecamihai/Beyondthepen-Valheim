using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public class PortalFollower : MonoBehaviour
{
    private Character m_character;
    private ZNetView m_nview;
    private Vector3 m_teleportTargetPos;
    private Quaternion m_teleportTargetRot;
    private GameObject playerGameObject;
    private FieldInfo m_bodyField;
    private Rigidbody m_body;

    private void Awake()
    {
        m_character = GetComponent<Character>();
        m_nview = GetComponent<ZNetView>();

        if (m_nview == null || m_character == null)
        {
            //Debug.LogError("[PortalFollower] Required components missing.");
            enabled = false;
            return;
        }

        // Access the private field m_body
        m_bodyField = typeof(Character).GetField("m_body", BindingFlags.NonPublic | BindingFlags.Instance);
        if (m_bodyField != null)
        {
            m_body = (Rigidbody)m_bodyField.GetValue(m_character);
            //Debug.Log("[PortalFollower] Successfully accessed m_body using reflection.");
        }
        else
        {
            //Debug.LogError("[PortalFollower] Failed to access m_body.");
            enabled = false;
            return;
        }

        //Debug.Log("[PortalFollower] Component attached to " + m_character.m_name);
    }

    public void UpdateTeleportPosition(Vector3 targetPosition, Quaternion targetRotation, GameObject player)
    {
        m_teleportTargetPos = targetPosition;
        m_teleportTargetRot = targetRotation;
        playerGameObject = player;
        StartCoroutine(UpdateTeleport());
    }

    private IEnumerator UpdateTeleport()
    {
        // Set position, rotation, and reset velocity if necessary
        if (m_nview.IsOwner())
        {
            m_character.transform.position = m_teleportTargetPos;
            m_character.transform.rotation = m_teleportTargetRot;

            if (m_body != null)
            {
                m_body.velocity = Vector3.zero;
            }
            else
            {
                //Debug.LogError("[PortalFollower] m_body is null. Velocity reset skipped.");
            }

            Vector3 direction = m_teleportTargetRot * Vector3.forward;
            m_character.SetLookDir(direction, 0f);

            // Ensure the animal's follow target is still the player after teleportation
            m_character.GetComponent<CTA>()?.m_TameableAI.SetFollowTarget(playerGameObject);
            //Debug.Log("[PortalFollower] Teleport successful. Final position: " + m_character.transform.position);
        }
        yield break;
    }

}
