using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.VisualScripting;

public class ButtonManager : MonoBehaviour
{

    [SerializeField] private float m_Speed = 5f;
    [SerializeField] private Transform m_Player;
    [SerializeField] private Animator m_Animator;
    [SerializeField] private Image m_BattleButton;
    [SerializeField] private GameObject m_ButtonPressed;
    [SerializeField] private float m_Duration = 1f;
    [SerializeField] private Material m_ButtonMaterial;
    [SerializeField] private float m_RaiseSpeed = 4f;
    [SerializeField] private float m_LowerSpeed = 2f;
    [SerializeField] private float m_WaveTopHeight = 0.4f;
    [SerializeField] private float m_WaveBottomHeight = 0f;

    [Header("UI")]
    [SerializeField] private PointToClick m_PointToClickPrefab;

    [Header("Particles")]
    [SerializeField] private ParticleSystem m_ClickParticleSystemPrefab;

    private Vector3? m_Destination;
    private Vector3 m_StartPos;
    private Vector2 m_ClickPosition;
    private float m_Timer;
    private float m_CurrentWaveHeight = 0.0f;
    private float m_TargetWaveHeight = 0.0f;

    private bool m_IsAttacking = false;
    private bool m_IsHolding = false;
    private bool m_IsHoldLoopActive = false;
    private bool m_ReadyToStopLoop = false;
    private bool m_CanPress = true;


    private Coroutine m_HoldAttackLoopCoroutine = null;

    void Start()
    {
        m_StartPos = m_Player.position;
        m_ButtonMaterial.SetFloat("_Wave_Height", m_WaveBottomHeight);
        Debug.Log("[Start] Wave Height Initialized to Bottom Height");
    }

    void Update()
    {
        // Movement
        if (m_Destination.HasValue)
        {
            var dir = m_Destination.Value - m_Player.position;
            m_Player.position += dir.normalized * Time.deltaTime * m_Speed;

            if (Vector3.Distance(m_Player.position, m_Destination.Value) < 0.1f)
            {
                m_Destination = null;
                StartCoroutine(TimerCoroutine(0.5f));
            }
        }

        // Wave height update
        float speed = m_TargetWaveHeight > m_CurrentWaveHeight ? m_RaiseSpeed : m_LowerSpeed;
        float prevHeight = m_CurrentWaveHeight;
        m_CurrentWaveHeight = Mathf.MoveTowards(m_CurrentWaveHeight, m_TargetWaveHeight, Time.deltaTime * speed);
        m_ButtonMaterial.SetFloat("_Wave_Height", m_CurrentWaveHeight);

        if (!Mathf.Approximately(prevHeight, m_CurrentWaveHeight))
        {
            Debug.Log($"[Wave] Current: {m_CurrentWaveHeight:F3} → Target: {m_TargetWaveHeight:F3} (Speed: {speed})");
        }

        // Input handling
        if (m_ButtonPressed != null)
        {
            OnButtonPress();
        }
    }

    private bool IsPointerOverUIObject(GameObject uiObject)
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);

        foreach (var result in raycastResults)
        {
            if (result.gameObject == uiObject) return true;
        }
        return false;
    }

    void DisplayClickEffect(Vector2 screenPoint)
    {
        Vector3 worldPos =
        Camera.main.ScreenToWorldPoint(new Vector3(
        screenPoint.x,
        screenPoint.y,
        Camera.main.nearClipPlane));
        worldPos.z = 0; // Optional: Set Z to 0 if you're working in 2D
        Instantiate(m_PointToClickPrefab, worldPos, Quaternion.identity);
        ParticleSystem particleEffect = Instantiate(m_ClickParticleSystemPrefab, worldPos, Quaternion.identity);
        particleEffect.Play();

        StartCoroutine(DestroyParticleSystemAfterEffect(particleEffect));
    }

    public void OnButtonPress()
    {


        if (!m_CanPress) return;

        bool isPointerOverButton = Input.GetMouseButton(0) && IsPointerOverUIObject(m_BattleButton.gameObject);

        if (Input.GetMouseButtonDown(0) && IsPointerOverUIObject(m_BattleButton.gameObject) && !m_IsHolding)
        {
            Debug.Log("[Input] Mouse Down → Set Wave Height Target = 1");
            m_TargetWaveHeight = m_WaveTopHeight;

            Vector3 screenPos = Input.mousePosition;
            DisplayClickEffect(screenPos);
        }

        if (isPointerOverButton)
        {
            m_BattleButton.color = new Color(1, 1, 1, 0);
            m_ButtonPressed?.SetActive(true);

            if (m_Timer >= m_Duration * 0.5f && !m_IsHoldLoopActive)
            {
                Debug.Log("[Hold] Timer exceeded -> Trigger hold effect loop");
                m_IsHoldLoopActive = true;
                m_ReadyToStopLoop = false;
                m_HoldAttackLoopCoroutine = StartCoroutine(HoldAttackLoop());
            }

            if (!m_IsHolding)
            {
                m_IsHolding = true;
                m_Timer = 0f;
                Debug.Log("[Input] Started Holding");
            }

            m_Timer += Time.deltaTime;
        }
        else
        {
            if (m_IsHolding)
            {
                m_IsHolding = false;

                if (m_IsHoldLoopActive && m_ReadyToStopLoop)
                {
                    Debug.Log("[Loop] Tapped to stop loop");
                    m_IsHoldLoopActive = false;

                    if (m_HoldAttackLoopCoroutine != null)
                    {
                        StopCoroutine(m_HoldAttackLoopCoroutine);
                        m_HoldAttackLoopCoroutine = null;
                    }
                    m_TargetWaveHeight = m_WaveBottomHeight;
                    StartCoroutine(ResetButtonAfter(m_Duration * 0.5f));
                }

                if (!m_IsHoldLoopActive)
                {
                    Debug.Log("[Press] Short press → Trigger one attack");
                    m_CanPress = false;
                    TriggerBattle();
                    m_ButtonPressed?.SetActive(true);
                    m_BattleButton.color = new Color(1, 1, 1, 0);
                    m_TargetWaveHeight = m_WaveBottomHeight;
                    StartCoroutine(ResetButtonAfter(m_Duration));
                }

                if (m_IsHoldLoopActive)
                {
                    Debug.Log("[Loop] Holding complete → Await tap to stop");
                    m_ReadyToStopLoop = true;
                }

                m_Timer = 0f;
            }
        }
    }

    public void TriggerBattle(bool fromLoop = false)
    {
        if (!fromLoop && m_IsHoldLoopActive)
        {
            Debug.Log("[Manual] Tap during loop → Stop loop");
            m_IsHoldLoopActive = false;

            if (m_HoldAttackLoopCoroutine != null)
            {
                StopCoroutine(m_HoldAttackLoopCoroutine);
                m_HoldAttackLoopCoroutine = null;
            }

            StartCoroutine(ResetButtonAfter(m_Duration));
            return;
        }

        if (m_IsAttacking) return;

        Debug.Log("[Battle] Triggering Attack");
        m_IsAttacking = true;
        m_Animator.SetTrigger("Attack");
        StartCoroutine(DelayedMoveAfterFrames(120));
    }

    private IEnumerator HoldAttackLoop()
    {
        while (m_IsHoldLoopActive)
        {
            Debug.Log("[Loop] Hold Attack");
            TriggerBattle(true);
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator TimerCoroutine(float waitTime)
    {
        Debug.Log("[Timer] Coroutine started");

        while (!m_Animator.GetCurrentAnimatorStateInfo(0).IsName("Attack_Light_01"))
            yield return null;

        while (m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.6f &&
               m_Animator.GetCurrentAnimatorStateInfo(0).IsName("Attack_Light_01"))
            yield return null;

        yield return new WaitForSeconds(waitTime);

        Debug.Log("[Timer] Returning to Start Pos");
        SetDestination(m_StartPos);
        m_IsAttacking = false;
    }

    private IEnumerator DelayedMoveAfterFrames(int frameDelay)
    {
        for (int i = 0; i < frameDelay; i++)
            yield return null;

        SetDestination(new Vector3(transform.position.x + 0.6f, m_Player.position.y, 0));
    }

    private IEnumerator ResetButtonAfter(float delay)
    {
        Debug.Log($"[Reset] Button reset in {delay} seconds");
        yield return new WaitForSeconds(delay);
        m_BattleButton.color = new Color(1, 1, 1, 1);
        m_ButtonPressed?.SetActive(false);
        m_CanPress = true;
        Debug.Log("[Reset] Button visible & clickable again");
    }

    public void SetDestination(Vector3 destination)
    {
        Debug.Log($"[Move] Set Destination: {destination}");
        m_Destination = destination;
    }

    private IEnumerator DestroyParticleSystemAfterEffect(ParticleSystem particleSystem)
    {
        // Wait for the particle system to finish playing
        while (particleSystem.isPlaying)
        {
            yield return null;
        }

        // Once done, destroy the particle system
        Destroy(particleSystem.gameObject);
    }

}
