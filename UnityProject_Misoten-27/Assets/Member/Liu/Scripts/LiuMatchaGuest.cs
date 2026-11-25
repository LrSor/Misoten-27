using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public class LiuMatchaGuest : MiniGameObject
{
    [SerializeField] private GameObject m_guestModel;
    [SerializeField] private GameObject m_stoolModel;
    [SerializeField] private AnimationClip m_walkClip;
    [Header("お客さんの入場")]
    [SerializeField, Min(0f)] private float m_arrivalDelay = 2f;
    [SerializeField, Min(0.1f)] private float m_walkSeconds = 2.4f;
    [SerializeField, Min(0.1f)] private float m_sitSeconds = 0.65f;
    [Tooltip("ミニゲームのローカル座標で指定する座席の中心")]
    [SerializeField] private Vector3 m_seat = new Vector3(0f, 0f, 1.35f);
    [SerializeField] private float m_entranceX = -4.6f;

    private const float SeatHeight = 0.45f;
    private Transform m_guest;
    private Animator m_animator;
    private Transform m_leftHip;
    private Transform m_rightHip;
    private PlayableGraph m_graph;
    private AnimationClipPlayable m_walk;
    private Limb[] m_limbs;
    private float m_elapsed;
    private bool m_running;
    private bool m_sitting;
    private Vector3 m_standingPosition;
    private Vector3 m_seatedPosition;

    // 着席に使う骨と、その子の骨が最後に向く方向（客のローカル空間）。
    private class Limb
    {
        public Transform joint;
        public Transform tip;
        public Vector3 seatedDirection;
        public Quaternion standingRotation;
        public Quaternion seatedRotation;
    }

    private void Awake()
    {
        if (m_guestModel == null || m_stoolModel == null || m_walkClip == null)
        {
            Debug.LogWarning("LiuMatchaGuest プレハブに客・椅子・歩行クリップを設定してください。", this);
            return;
        }

        var stool = Instantiate(m_stoolModel, transform, false);
        stool.name = "Stool";
        // stoolBar の原点は座面の中心ではなく脚の角。モデルの実寸から補正する。
        Bounds bounds = stool.GetComponent<MeshFilter>().sharedMesh.bounds;
        float widthScale = 0.48f / bounds.size.x;
        var stoolScale = new Vector3(widthScale, SeatHeight / bounds.size.y, widthScale);
        stool.transform.localRotation = Quaternion.identity;
        stool.transform.localScale = stoolScale;
        var footCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        stool.transform.localPosition = m_seat - Vector3.Scale(footCenter, stoolScale);

        m_guest = new GameObject("Guest").transform;
        m_guest.SetParent(transform, false);
        var model = Instantiate(m_guestModel, m_guest, false);
        model.transform.localScale *= 0.84f;
        m_animator = model.GetComponentInChildren<Animator>();
        if (m_animator == null || !m_animator.isHuman)
        {
            Debug.LogWarning("お客さんには Humanoid のモデルを使用してください。", this);
            m_guest.gameObject.SetActive(false);
            return;
        }

        m_animator.applyRootMotion = false;
        m_animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        m_graph = PlayableGraph.Create("Matcha guest walk");
        m_graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        m_walk = AnimationClipPlayable.Create(m_graph, m_walkClip);
        var output = AnimationPlayableOutput.Create(m_graph, "Walk", m_animator);
        output.SetSourcePlayable(m_walk);
        m_graph.Play();
        m_graph.Evaluate(0f);

        m_leftHip = m_animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        m_rightHip = m_animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        m_limbs = new[]
        {
            Link(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, -0.13f, -0.06f, 1f),
            Link(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0f, -1f, 0.10f),
            Link(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, 0f, -0.10f, 1f),
            Link(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.13f, -0.06f, 1f),
            Link(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0f, -1f, 0.10f),
            Link(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, 0f, -0.10f, 1f),
            Link(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -0.18f, -1f, 0.34f),
            Link(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.24f, -0.28f, 1f),
            Link(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.18f, -1f, 0.34f),
            Link(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, -0.24f, -0.28f, 1f)
        };
        m_guest.gameObject.SetActive(false);
    }

    private Limb Link(HumanBodyBones joint, HumanBodyBones tip, float x, float y, float z)
    {
        return new Limb
        {
            joint = m_animator.GetBoneTransform(joint),
            tip = m_animator.GetBoneTransform(tip),
            seatedDirection = new Vector3(x, y, z)
        };
    }

    public override void Activate()
    {
        base.Activate();
        if (m_limbs == null)
            return;
        m_guest.gameObject.SetActive(false);
        m_elapsed = 0f;
        m_sitting = false;
        m_running = true;
    }

    public override void UpdateObject()
    {
        base.UpdateObject();
        Step(Time.deltaTime);
    }

    private void Step(float seconds)
    {
        if (!m_running) return;
        m_elapsed += seconds;
        float walkingTime = m_elapsed - m_arrivalDelay;
        if (walkingTime < 0f) return;

        if (!m_sitting)
        {
            if (!m_guest.gameObject.activeSelf)
                m_guest.gameObject.SetActive(true);
            float t = Mathf.Clamp01(walkingTime / m_walkSeconds);
            var start = m_seat + new Vector3(m_entranceX, 0f, -0.22f);
            var end = m_seat + Vector3.back * 0.22f;
            m_guest.localPosition = Vector3.Lerp(start, end, t);
            float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, t));
            m_guest.localRotation = Quaternion.Euler(0f, Mathf.Lerp(90f, 180f, turn), 0f);
            m_walk.SetTime(Mathf.Min(walkingTime, m_walkSeconds) * 1.3f);
            m_graph.Evaluate(0f);
            if (t < 1f) return;
            PrepareSeat();
        }

        float progress = Mathf.Clamp01((walkingTime - m_walkSeconds) / m_sitSeconds);
        float blend = Mathf.SmoothStep(0f, 1f, progress);
        m_guest.localPosition = Vector3.Lerp(m_standingPosition, m_seatedPosition, blend);
        foreach (var limb in m_limbs)
        {
            if (limb.tip != null && limb.joint != null)
                limb.joint.localRotation = Quaternion.Slerp(limb.standingRotation, limb.seatedRotation, blend);
        }
        if (progress >= 1f) m_running = false;
    }

    private void PrepareSeat()
    {
        m_sitting = true;
        m_standingPosition = m_guest.localPosition;
        Vector3 hipCenter = transform.InverseTransformPoint((m_leftHip.position + m_rightHip.position) * 0.5f);
        m_seatedPosition = m_standingPosition + (m_seat + Vector3.up * (SeatHeight + 0.07f) - hipCenter);

        // 親の骨から順に最終姿勢を求め、最後の歩行姿勢から滑らかに着席させる。
        foreach (var limb in m_limbs)
        {
            if (limb.tip == null || limb.joint == null) continue;
            limb.standingRotation = limb.joint.localRotation;
            Vector3 current = limb.tip.position - limb.joint.position;
            Vector3 target = m_guest.TransformDirection(limb.seatedDirection);
            limb.joint.rotation = Quaternion.FromToRotation(current, target) * limb.joint.rotation;
            limb.seatedRotation = limb.joint.localRotation;
        }
    }

    public override void Deactivate()
    {
        base.Deactivate();
        m_running = false;
        if (m_guest != null) m_guest.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (m_graph.IsValid()) m_graph.Destroy();
    }
}
