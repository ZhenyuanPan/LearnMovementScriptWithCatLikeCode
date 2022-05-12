using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingSphere : MonoBehaviour
{
    [SerializeField, Range(0f,100f)]
    float maxSpeed = 10f;

    [SerializeField, Range(0, 100f)]
    float maxAcceleration = 10f;

    [SerializeField]
    Rect allowedArea = new Rect(-4.5f, -4.5f, 9f, 9f);

    [SerializeField, Range(0f,1f)]
    float bounciness = 0.5f;//弹力系数

    Vector3 velocity;//当前速度



    void Update()
    {
        Vector2 playerInput;
        playerInput.x = 0f;
        playerInput.y = 0f;
        playerInput.x = Input.GetAxis("Horizontal");
        playerInput.y = Input.GetAxis("Vertical");
        playerInput =  Vector2.ClampMagnitude(playerInput,1f);
        //期望速度
        Vector3 desiredVelocity = new Vector3(playerInput.x, 0, playerInput.y) * maxSpeed;
        //最大速度变化
        float maxSpeedChange = maxAcceleration * Time.deltaTime;
        //防止超调
        velocity.x = Mathf.MoveTowards(velocity.x,desiredVelocity.x,maxSpeedChange);
        velocity.z = Mathf.MoveTowards(velocity.z,desiredVelocity.z,maxSpeedChange);

        Vector3 displacement = velocity * Time.deltaTime;

        Vector3 newPosition = transform.localPosition + displacement;
        //限制小球在rect内
        if (newPosition.x < allowedArea.xMin)
        {
            newPosition.x = allowedArea.xMin;
            velocity.x = -velocity.x*bounciness;
        }
        else if (newPosition.x > allowedArea.xMax)
        {
            newPosition.x = allowedArea.xMax;
            velocity.x = -velocity.x*bounciness;
        }
        if (newPosition.z < allowedArea.yMin)
        {
            newPosition.z = allowedArea.yMin;
            velocity.z = -velocity.z*bounciness;
        }
        else if (newPosition.z > allowedArea.yMax)
        {
            newPosition.z = allowedArea.yMax;
            velocity.z = -velocity.z*bounciness;
        }
        transform.localPosition = newPosition;
    
    }
}
 