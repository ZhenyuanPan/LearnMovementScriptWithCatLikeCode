using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingSphere : MonoBehaviour
{
    Rigidbody body; //小球的刚体

    [SerializeField, Range(0f,100f)]
    float maxSpeed = 10f;

    [SerializeField, Range(0, 100f)]
    float maxAcceleration = 10f;

    Vector3 velocity;//当前速度

    Vector3 desiredVelocity;//期望速度

    bool desiredJump;//期望跳跃

    [SerializeField, Range(0f,10f)]
    float jumpHeight = 2f;//跳跃的高度

    //bool onGround; //小球是否在地面的标志值

    int groundContactCount; //地面接触点 数量
    bool OnGround => groundContactCount > 0; // 若是地面接触点>0 则OnGround值为true

    [SerializeField, Range(0,5)]
    int maxAirJumps;//空中多段跳跃次数

    int jumpPhase;//跳跃阶段, 用于记录多段跳

    [SerializeField, Range(0f, 100f)]
    float maxAirAcceleration = 1f; //最大空中加速度

    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f; //最大地面角度

    float minGroundDotProduct;//根据设置的最大地面角度计算点积

    Vector3 contactNormal;//接触面的法线

    private void Awake()
    {
        body = this.GetComponent<Rigidbody>();
        OnValidate();
    }

    private void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    /// <summary>
    /// 检测用户输入 在Update中
    /// </summary>
    void Update()
    {
        #region 玩家移动输入检测
        Vector2 playerInput;
        playerInput.x = 0f;
        playerInput.y = 0f;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);
        //期望速度
        desiredVelocity = new Vector3(playerInput.x, 0, playerInput.y) * maxSpeed;
        #endregion

        #region 玩家跳跃输入检测
        desiredJump = Input.GetButtonDown("Jump") | desiredJump;
        #endregion
    }

    /// <summary>
    /// 控制刚体速度在 fixupdate中
    /// </summary>
    private void FixedUpdate()
    {
        //多段跳的方法
        UpdateState();

        //新的修改小球速度的方法，根据接触平面映射x,z轴速度
        AdjustVelocity();

        #region 小球跳跃代码
        if (desiredJump)
        {
            desiredJump = false;
            Jump();
        }
        #endregion

        body.velocity = velocity;//给小球的速度赋值

        ClearState();//清空小球当前状态
    }

    /// <summary>
    /// 清空小球当前状态函数
    /// 包含设置接触地面点 为 0, 接触面法线为0
    /// </summary>
    void ClearState() 
    {
        groundContactCount = 0;
        contactNormal = Vector3.zero;
    }

    /// <summary>
    /// 多段跳方法
    /// </summary>
    void UpdateState() 
    {
        //因为碰撞等因素, 也会影响物体的刚体速度。所以在影响刚体速度之前要使我们的velocity = 小球刚体的速度
        velocity = body.velocity;
        if (OnGround)
        {
            jumpPhase = 0;
            if (groundContactCount > 1) //如果地面接触点大于1的时候才会处理平均平面的法向量, 因为1的话只接触一个面, 自然就是该面的法向量
            {
                contactNormal.Normalize(); //标准化这些接触点法向量的合集, 得到平均平面的法向量
            }
        }
        else 
        {
            contactNormal = Vector3.up;//在空中的话自然向上跳跃。
        }
    }

    /// <summary>
    /// 跳跃方法
    /// 根据公式 Vy = sqrt(-2gh)
    /// </summary>
    void Jump() 
    {
        if (OnGround || jumpPhase < maxAirJumps)
        {
            jumpPhase += 1;
            float jumpSpeed =  Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            if (velocity.y>0f)
            {
                jumpSpeed = Mathf.Max(jumpSpeed - velocity.y, 0f);
            }
            #region 竖直跳跃逻辑
            //velocity.y += jumpSpeed; 
            #endregion

            #region 背离接触面跳跃逻辑
            velocity += contactNormal * jumpSpeed;
            #endregion
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    /// <summary>
    /// 处理碰撞点方法
    /// </summary>
    /// <param name="collision"></param>
    void EvaluateCollision(Collision collision) 
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;

            if (normal.y >= minGroundDotProduct)
            {
                //onGround = true;
                groundContactCount += 1; //地面接触点每处理一个就需要+1
                contactNormal += normal;
            }
        }
    }

    /// <summary>
    /// 任意向量 映射到小球接触平面
    /// </summary>
    /// <param name="vector"></param>
    /// <returns></returns>
    Vector3 ProectOnContactPlane(Vector3 vector) 
    {
        return vector - contactNormal * Vector3.Dot(vector,contactNormal);
    }

    /// <summary>
    /// 在接触平面上投射小球速度
    /// </summary>
    void AdjustVelocity() 
    {
        //当前小球所接触平面投射过来的x, z轴
        Vector3 xAxis = ProectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProectOnContactPlane(Vector3.forward).normalized;
        //小球投射在当前平面上的x,z轴速度
        float currentX = Vector3.Dot(velocity,xAxis);
        float currentZ = Vector3.Dot(velocity,zAxis);

        //根据加速度 计算 新的瞬时速度（x，z轴）。
        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.deltaTime;
        float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);
        //根据新的瞬时速度和当前速度的差 也就是速度的增量（x，z轴） 调整当前速度
        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }

}
 