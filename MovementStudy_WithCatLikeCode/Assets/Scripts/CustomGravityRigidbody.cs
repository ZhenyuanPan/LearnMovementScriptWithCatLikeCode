using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CustomGravityRigidbody : MonoBehaviour
{
    Rigidbody body;//默认unity rigidbody
    float floatDelay;//休眠延迟
    [SerializeField]
    bool floatToSleep = false;//决定物体是否去睡眠, short-lived物体不需要去考虑睡眠状态
    private void Awake()
    {
        body = this.GetComponent<Rigidbody>();
        body.useGravity = false; //默认重力影响,设置为false
    }

    private void FixedUpdate()
    {

        if (floatToSleep)
        {
            if (body.IsSleeping()) //如果物体处于平衡, 休眠状态, 直接返回
            {
                floatDelay = 0f;
                return;
            }
            if (body.velocity.sqrMagnitude < 0.0001f) //当物体速度非常小的时候, 让物体休眠
            {
                floatDelay += Time.deltaTime;
                if (floatDelay >= 1f) //如果物体低速>阈值1秒钟, 则直接返回, 也就是让物体休眠
                {
                    return;
                }
            }
            else
            {
                floatDelay = 0f;
            }
        }

        body.AddForce(CustomGravity.GetGravity(body.position),ForceMode.Acceleration);//万象天引力
    }

    
}
