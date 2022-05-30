using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    [SerializeField]
    Transform focus = default;//摄像机焦点对象
    Vector3 focusPoint;//摄像机当前帧焦点位置
    Vector3 previousFocusPoint;//摄像机上一帧焦点位置
    [SerializeField, Range(1f, 10f)]
    float distance = 5f;//摄像机与焦点的距离
    [SerializeField, Min(0f)]
    float focusRadius = 1f;//焦距半径, 如果超过半径则摄像机就会移动
    [SerializeField, Range(0f, 1f)]
    float focusCentering = 0.5f;//拉回相机焦点中心系数
    [SerializeField]
    Vector2 orbitAngles = new Vector2(45f, 0f);//摄像机旋转角度 默认值(45,0)代表着垂直斜向下45度,水平正对着z轴
    [SerializeField, Range(1f, 360f)]
    float rotationSpeed = 90f;//旋转速度, 度每秒
    [SerializeField, Range(-89f, 89f)]
    float minVerticalAngle = -30f, maxVerticalAngle = 60f;//限制相机垂直方向的最大旋转角度
    [SerializeField, Min(0f)]
    float alignDelay = 5f;//自动镜头调整延迟
    [SerializeField, Range(0f, 90f)]
    float alignSmoothRange = 45f;//在此角度之前自动调整摄像机都使用线性的旋转速度, 否则全速旋转
    float lastManualRotationTime;//最后一次手动调整镜头的事件
    Camera regularCamera;//当前Camera

    Quaternion gravityAlignment = Quaternion.identity;//与重力方向平行但方向相反 的 四元数

    Quaternion orbitRotation;//用来跟踪记录, orbit rotation的四元数

    Vector3 CameraHalfExtends //box cast所需要的3dvector属性
    {
        get 
        {
            Vector3 halfExtends;
            halfExtends.y = regularCamera.nearClipPlane * Mathf.Tan(0.5f*Mathf.Deg2Rad*regularCamera.fieldOfView);
            halfExtends.x = halfExtends.y * regularCamera.aspect;
            halfExtends.z = 0f;
            return halfExtends;
        }
    }

    [SerializeField]
    LayerMask obstructionMask = -1;//box cast layermask


    private void OnValidate()
    {
        if (maxVerticalAngle<minVerticalAngle)//注意限制要求, 最大垂直旋转角度不可比最小垂直旋转角度小
        {
            maxVerticalAngle = minVerticalAngle;
        }
    }

    private void Awake()
    {
        regularCamera = this.GetComponent<Camera>();//获取当前Camera组件
        focusPoint = focus.position; //游戏开始时候初始化摄像机位置, 使得摄像机焦点中心对应focus
        transform.localRotation = orbitRotation = Quaternion.Euler(orbitAngles);//摄像机旋转初始化, 同时链式声明orbitRotation的初始化
        
    }
    private void LateUpdate()
    {
        gravityAlignment = Quaternion.FromToRotation(gravityAlignment * Vector3.up, CustomGravity.GetUpAxis(focusPoint)) * gravityAlignment;//每一帧都根据重力的方向平行对齐 新的Up direction。

        UpdateFocusPoint();
        if (ManualRotation() || AutomaticRotation())//如果发生了手动控制旋转 或者发生了自动调整调整旋转
        {
            ConstrainAngels();//限制旋转角度
            orbitRotation = Quaternion.Euler(orbitAngles);//记录下 初始重力坐标系下的 orbitRotation
        }
        Quaternion lookRotation = gravityAlignment * orbitRotation;//转换到新的重力坐标系下的 摄像机视角朝向。
        
        Vector3 lookDirection = lookRotation * Vector3.forward;//z轴方向为默认正方向,也就是vector3.forward), 将四元数左乘该向量, 表示将该向量进行旋转。得到摄像机镜头方向
        Vector3 lookPostion = focusPoint - lookDirection * distance;//根据镜头距离(向量模长)和镜头方向(向量方向)算得摄像机的位置(摄像机初识位置是原点,所以摄像机位置lookPostion=p0+lookVector = lookVecter)

        Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane; //box cast 的rect相对于相机镜头位置的位置偏移 =>在相机镜头方向上 大小为近裁剪面距离的向量 
        Vector3 rectPosition = lookPostion + rectOffset; //box cast的 rect 的位置 => 摄像机的位置往前移动一个近裁剪面距离的位置 
        Vector3 castFrom = focus.position;//box cast的开始位置, 也就是焦点位置
        Vector3 castLine = rectPosition - castFrom;//box cast 检测的向量(rect 深度向量)
        float castDistance = castLine.magnitude;//box cast 检测的距离。
        Vector3 castDirection = castLine / castDistance;//box cast 检测的方向

        if (Physics.BoxCast(castFrom,CameraHalfExtends,castDirection,out RaycastHit hit,lookRotation,castDistance,obstructionMask))//使用盒子检测,基于摄像机近裁剪平面制作的盒子
        {
            rectPosition = castFrom + castDirection * hit.distance;//当hit检测到是box cast的位置 
            lookPostion = rectPosition - rectOffset;//box cast的位置往后挪一个 box cast rect相对于相机镜头位置的位置偏移。就得到了镜头位置
        }

        transform.SetPositionAndRotation(lookPostion,lookRotation);//对摄像机位置和旋转属性赋值。
    }
    /// <summary>
    /// 摄像机跟随效果
    /// 包括摄像机焦距半径
    /// 缓慢看向焦点
    /// </summary>
    void UpdateFocusPoint() 
    {
        previousFocusPoint = focusPoint;//记录上一帧的FocusPoint位置

        Vector3 targetPoint = focus.position;//焦点物体的位置
        if (focusRadius > 0f)
        {
            float distance = Vector3.Distance(targetPoint, focusPoint);//焦点物体的位置和当前摄像机焦点的距离
            float t = 1f;
            if (distance > 0.01f && focusCentering >0f) //如果该距离>0.01f,并且拉回相机焦点中心系数>0的话
            {
                t = Mathf.Pow(1f - focusCentering, Time.unscaledDeltaTime);//t = (1-c)^deltaTime
            }
            if (distance > focusRadius)//如果该距离>设置好的焦距半径
            {
                t = Mathf.Min(t, focusRadius/distance);//使用interpolater的最小值, 让摄像机焦点尽可能对着焦点物体的位置，这就是焦点居中效果。
            }
            /*
             * 目前的效果对于小球teleport效果时候, 会是这样的
             * 先是瞬间被拉到 摄像机焦点（focusPoint）到 焦点物体位置targetPoint+焦距半径focusRadius处。然后慢慢焦点居中
             */
            focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);//给当前帧焦点位置 赋值
        }
        else
        {
            focusPoint = targetPoint;//给当前帧焦点位置赋值
        }
    }


    /// <summary>
    /// 手动鼠标控制管理镜头旋转
    /// </summary>
    bool ManualRotation() 
    {
        Vector2 input = new Vector2(
            Input.GetAxis("Vertical Camera"),
            Input.GetAxis("Horizontal Camera")
        );
        const float e = 0.001f;//鼠标输入阈值, 超过此阈值则对镜头旋转进行控制
        if (input.x < -e || input.x>e||input.y<-e||input.y>e)
        {
            orbitAngles += rotationSpeed * Time.unscaledDeltaTime * input;
            lastManualRotationTime = Time.unscaledTime;//记录手动调整镜头的事件
            return true;
        }
        return false;
    }

    /// <summary>
    /// 限制摄像机转动角度
    /// </summary>
    void ConstrainAngels() 
    {
        //因为orbitAngles的x分量代表着摄像机垂直方向的旋转角度, 所以需要根据我们的配置加以限制
        orbitAngles.x = Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);
        //将摄像机水平的角度 限制在0到360度之间
        if (orbitAngles.y<0f)
        {
            orbitAngles.y += 360f;
        }
        else if (orbitAngles.y>=360f)
        {
            orbitAngles.y -= 360f;
        }

    }

    /// <summary>
    /// 自动调整镜头旋转
    /// </summary>
    bool AutomaticRotation() 
    {
        if (Time.unscaledTime - lastManualRotationTime < alignDelay)
        {
            return false;
        }
        Vector3 alignedDelta = Quaternion.Inverse(gravityAlignment) * (focusPoint - previousFocusPoint);//将上一帧摄像机焦点位置 和当前帧摄像机焦点位置 的位移 转到新的重力坐标系下
        //计算上一帧 和当前帧x,z平面(当前重力坐标系下的xz平面)上移动的向量。
        Vector2 movement = new Vector2(alignedDelta.x, alignedDelta.z);
        float movementDeltaSqr = movement.sqrMagnitude;//计算上一帧和当前帧位移向量大小的平方值
        if (movementDeltaSqr <0.0001f)//如果上一帧和当前帧之间的xz平面位移实在太小则忽略
        {
            return false;
        }

        float headingAngle = GetAngle(movement/Mathf.Sqrt(movementDeltaSqr));//根据位移向量1 和位移向量的模长, 计算位移向量的单位向量 也就是位移方向, 根据位移方向获得, 相对于z轴(z轴为0度)的角度

        /*
         * 摄像机需要旋转的角度绝对值 计算以度数表示的两个给定角(角度值)之间的最小差值。
         * 解析Mathf.DeltaAngle函数
         * 例如果参数1=0, 参数2=270, 则DeltaAngel值= -90
         * 也就是参数1和参数2之间的夹角。顺时针为正, 逆时针为负
         */
        float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y,headingAngle));

        float rotationChange = rotationSpeed * Mathf.Min(Time.unscaledDeltaTime, movementDeltaSqr);//如果运动过于微小, 则应该更进一步的缩放旋转速度
        if (deltaAbs < alignSmoothRange)//如果摄像机需要旋转的角度绝对值小于平滑旋转角度范围, 则采用线性配置的旋转速度
        {
            rotationChange = rotationChange * deltaAbs / alignSmoothRange;
        }
        else if ((180f - deltaAbs) < alignSmoothRange)//如果焦点物体朝向摄像机移动, 则deltaAbs = 180f, 180-deltaAbs = 0; 也就是焦点物体朝向摄像机移动时候不会发生摄像机水平旋转
        {

            rotationChange = rotationChange * (180f - deltaAbs) / alignSmoothRange;
        }

        orbitAngles.y = Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange);//调整摄像头水平旋转角度
        return true;
    }


    /// <summary>
    /// 根据二维向量,获取角度
    /// 本来左右的角度都用0-180度表示, 我们把它转变为360度角, x为正,为0,179, x为负为180到359, 这表示顺时针圆周运动
    /// 数学公式映射到顺时针圆周运动=>当x轴为负数 则返回 360-angle,否则直接返回angle
    /// </summary>
    /// <returns></returns>
    static float GetAngle(Vector2 direction) 
    {
        float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
        return direction.x < 0f ? 360f - angle : angle;//x值为负 则是逆时针转动, x值为正则为顺时针转动,
    }

    

}
