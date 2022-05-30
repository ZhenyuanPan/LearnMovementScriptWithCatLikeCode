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

    int groundContactCount; //地面接触点 数量

    int steepContactCount; //陡峭面接触点 数量

    bool OnGround => groundContactCount > 0; // 若是地面接触点>0 则OnGround值为true

    bool OnSteep => steepContactCount > 0;// 若是陡峭面接触点>0 则OnSteep值为true

    [SerializeField, Range(0,5)]
    int maxAirJumps;//空中多段跳跃次数

    int jumpPhase;//跳跃阶段, 用于记录多段跳

    [SerializeField, Range(0f, 100f)]
    float maxAirAcceleration = 1f; //最大空中加速度

    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f; //最大地面角度
    [SerializeField, Range(0f, 90f)]
    float maxStairsAngle = 50f; //最大可以攀爬楼梯角度
    
    float minGroundDotProduct;//根据设置的最大地面坡度角度计算点积
    float minStairsDotProduct; //根据设置的最大攀爬楼梯角度 计算点积

    Vector3 contactNormal;//接触面的法线

    Vector3 steepNormal;//接触的陡峭面的法线

    int stepsSinceLastGround;//自上一次接触地面之后经历过的物理步数
    int stepsSinceLastJump; //自从上一次跳跃后经历的物理步数

    [SerializeField, Range(0f,100f)]
    float maxSnapSpeed = 100f; //保持可以在斜面运动的最大速度
  
    [SerializeField, Min(0f)]
    float probeDistance = 1f;//SnapToGround 效果 的射线检测的距离

    [SerializeField]
    LayerMask probeMask = -1;//小球可以探索层级 => 在地面上
    [SerializeField]
    LayerMask stairsMask = -1;//小球可以探索层级 => 在楼梯上

    [SerializeField]
    Transform playerInputSpace = default;//用户移动输入空间

    Vector3 upAxis;// 用于custom gravity的y轴, 他根据重力的方向改变
    Vector3 rightAxis;//用于custom gravity的x轴
    Vector3 forwardAxis;//用于custom gravity的z轴
    private void Awake()
    {
        body = this.GetComponent<Rigidbody>();
        body.useGravity = false;//关闭 默认环境下重力 对小球的影响, 因为我们使用的是自定义重力
        OnValidate();
    }

    private void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
    }

 
    /// <summary>
    /// 控制刚体速度在 fixupdate中
    /// </summary>
    private void FixedUpdate()
    {
        upAxis = -Physics.gravity.normalized;//目前upAxis指向重力的反方向

        Vector3 gravity = CustomGravity.GetGravity(body.position,out upAxis);//获得当前物理帧中 使用的重力, 和与重力平行的up轴

        //多段跳的方法
        UpdateState();

        //新的修改小球速度的方法，根据接触平面映射x,z轴速度
        AdjustVelocity();

        #region 小球跳跃代码
        if (desiredJump)
        {
            desiredJump = false;
            Jump(gravity);
        }
        #endregion

        velocity += gravity * Time.deltaTime;//把当前物理帧中的 重力作为加速度 影响当前速度, v1 = v0 + at

        body.velocity = velocity;//给小球的速度赋值

        ClearState();//清空小球当前状态
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
        playerInput = Vector2.ClampMagnitude(playerInput, 1f);//钳主playerInput向量的模长为1, 使其不能输出超过预设的最大速度
        //期望速度
        if (playerInputSpace)//如果InputSpace被设置了
        {
            rightAxis = ProjectDirectionOnPlane(playerInputSpace.right,upAxis);//投影用户输入空间中的right轴 到 gravity-relative 空间中的xz平面上
            forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward,upAxis);//同理投影用户输入空间中的forward轴到 gravity-relative 空间中的xz平面上
        }
        else//如果InputSpace没有被设置, 则直接把世界空间的right 和forward轴 投影到 gravity-relative的xz平面上
        {
            rightAxis = ProjectDirectionOnPlane(Vector3.right,upAxis);
            forwardAxis = ProjectDirectionOnPlane(Vector3.forward,upAxis);
        }
        #endregion


        desiredVelocity = new Vector3(playerInput.x,0f,playerInput.y) * maxSpeed;

        #region 玩家跳跃输入检测
        desiredJump = Input.GetButtonDown("Jump") | desiredJump;
        #endregion

        this.GetComponent<Renderer>().material.SetColor("_Color", OnGround ? Color.black : Color.white);

    }


    /// <summary>
    /// 多段跳方法
    /// </summary>
    void UpdateState()
    {
        stepsSinceLastGround += 1;//在下一次落地之前, 没经历一次phsics step都会使其+1
        stepsSinceLastJump += 1;//在下一次跳跃之前, 每经历一次phsics step都会使该变量+1
        velocity = body.velocity; //因为碰撞等因素, 也会影响物体的刚体速度。所以在影响刚体速度之前要使我们的velocity = 小球刚体的速度
        if (OnGround || SnapToGround() || CheckSteepContacts())//注意||符号的执行逻辑, 如果OnGround=true则跳过||后的语句,直接进入下面的语句。如果||左边为flase，则会执行||右边的语句,然后得出||结果
        {
            stepsSinceLastGround = 0; // 如果落地了, 则设置steps数为0
            /*
             * 小球获得跳跃速度的时候 stepSinceLastJump =1, 
             * 但是小球并没有真正的移动，小球位置因为跳跃位置移动是在step>1的物理帧中。
             * 这里的意思就是,当小球接受到跳跃指令后, 在真正移动的物理帧中如果碰到了地面 此时才能把jumpPhase(当前连跳次数)设置为0
             */
            if (stepsSinceLastJump > 1)
            {
                jumpPhase = 0;
            }
            if (groundContactCount > 1) //如果地面接触点大于1的时候才会处理平均平面的法向量, 因为1的话只接触一个面, 自然就是该面的法向量
            {
                contactNormal.Normalize(); //标准化这些接触点法向量的合集, 得到平均平面的法向量
            }
        }
        else
        {
            contactNormal = upAxis;//在空中的话自然向上跳跃。
        }
    }


    /// <summary>
    /// 在接触平面上投射小球速度
    /// </summary>
    void AdjustVelocity()
    {
        
        //将小球rightAxis 和 forwardAxis(根据gravity方向变化的两个轴)投影到小球接触平面上
        Vector3 xAxis = ProjectDirectionOnPlane(rightAxis, contactNormal);
        Vector3 zAxis = ProjectDirectionOnPlane(forwardAxis, contactNormal);

        //小球投射在当前平面上的x,z轴速度
        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        //根据加速度 计算 新的瞬时速度（x，z轴）。
        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.deltaTime;
        float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);
        //根据新的瞬时速度和当前速度的差 也就是速度的增量（x，z轴） 调整当前速度
        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }

    /// <summary>
    /// 跳跃方法
    /// 根据公式 Vy = sqrt(-2gh)
    /// 有一个参数用来接收当前物理帧 所使用的重力
    /// </summary>
    void Jump(Vector3 gravity)
    {
        Vector3 jumpDirection;
        if (OnGround)
        {
            jumpDirection = contactNormal;
        }
        else if (OnSteep)
        {
            jumpDirection = steepNormal;
            jumpPhase = 0;
        }
        else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps)
        {
            if (jumpPhase == 0)
            {
                jumpPhase = 1;
            }
            jumpDirection = contactNormal;
        }
        else
        {
            return;
        }

        stepsSinceLastJump = 0;
        jumpPhase += 1;
        float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);//根据gravity(当前物理帧 所使用重力)参数 来计算jumpSpeed
        jumpDirection = (jumpDirection + upAxis).normalized;//爬墙跳
        float alignedSpeed = Vector3.Dot(velocity, jumpDirection); //把Velocity 映射到jumpDir方向之后的 大小
        if (alignedSpeed > 0f)
        {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
        }
        velocity += jumpDirection * jumpSpeed;
    }


    /// <summary>
    /// 清空小球当前状态函数
    /// 包含设置接触地面点 为 0, 接触面法线为0
    /// </summary>
    void ClearState() 
    {
        groundContactCount = 0;
        steepContactCount = 0;
        contactNormal = Vector3.zero;
        steepNormal = Vector3.zero;
    }


    /// <summary>
    /// 处理碰撞点方法
    /// </summary>
    /// <param name="collision"></param>
    void EvaluateCollision(Collision collision) 
    {
        float minDot = GetMinDot(collision.gameObject.layer);
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            float upDot = Vector3.Dot(upAxis,normal);
            if (upDot >= minDot)
            {
                //onGround = true;
                groundContactCount += 1; //地面接触点每处理一个就需要+1
                contactNormal += normal;
            }
            else if (upDot > - 0.01f) //越陡, 则normal.y越接近于0
            {
                steepContactCount += 1;
                steepNormal += normal;
            }
        }
    }

    /// <summary>
    /// 映射方向 到 指定平面
    /// </summary>
    /// <param name="direction">方向</param>
    /// <param name="normal">平面的法线</param>
    /// <returns></returns>
    Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal) 
    {
        return (direction - normal * Vector3.Dot(direction,normal)).normalized;
    }

    /// <summary>
    /// 粘连在地面上
    /// </summary>
    /// <returns></returns>
    bool SnapToGround() 
    {
        if (stepsSinceLastGround > 1 || stepsSinceLastJump <= 2)
        {
            return false;
        }

        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed) 
        {
            return false;
        }

        if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hit,probeDistance, probeMask))
        {
            return false;
        }
        float upDot = Vector3.Dot(upAxis,hit.normal);//两个方向 也就是单位向量的点积 就是两向量夹角的cos值
        if (upDot < GetMinDot(hit.collider.gameObject.layer))//这里的判断逻辑是, 如果hit的平面的坡度大于我们认为是地面的坡度, 就不视为hit平面为ground, 所以直接返回
        {
            return false;
        }

        groundContactCount = 1;
        contactNormal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);
        /*
         * 把速度投影到跟地面方向一致, 然后保持之前的速度。
         */
        if (dot > 0f)
        {
            velocity = (velocity - hit.normal * dot).normalized * speed;
        }
        return true;
    }


    /// <summary>
    /// 根据layer层, 采用哪个点积(地面or楼梯)
    /// 如果layer形参 包含 starisMask层，则 (stairsMask & (1<<layer))！=0
    /// </summary>
    /// <param name="layer"></param>
    /// <returns></returns>
    float GetMinDot(int layer) 
    {
        return (stairsMask & (1<<layer)) == 0 ? minGroundDotProduct : minStairsDotProduct;
    }

    /// <summary>
    /// 它返回的是是否把小球接触的steepcontect 转换成一个 虚拟的groundContact
    /// </summary>
    /// <returns></returns>
    bool CheckSteepContacts() 
    {
        if (steepContactCount >1)
        {
            steepNormal.Normalize();

            float upDot = Vector3.Dot(upAxis , steepNormal);//平面法线和,upAxis的点积 作为判断依据

            if (upDot >= minGroundDotProduct)//如果综合起来steep contact的坡度比视作地面的坡度平摊。
            {
                steepContactCount = 0;
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }
        return false;
    }
}
 