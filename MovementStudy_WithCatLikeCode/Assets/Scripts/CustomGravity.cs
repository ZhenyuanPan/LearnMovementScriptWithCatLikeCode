using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CustomGravity 
{


    /// <summary>
    /// 获取自定义重力向量的静态方法
    /// </summary>
    /// <returns></returns>
    public static Vector3 GetGravity(Vector3 position)
    {
        return position.normalized * Physics.gravity.y;
    }

    /// <summary>
    /// 获取自定义重力向量的静态方法, 有一个OUT 参数 用来返回根据重力 决定的 up轴, GetGravity的重载方法overload
    /// </summary>
    /// <returns></returns>
    public static Vector3 GetGravity(Vector3 position , out Vector3 upAxis) 
    {
        Vector3 up = position.normalized;
        upAxis = Physics.gravity.y < 0f ? up : -up;
        return up * Physics.gravity.y;
    }


    /// <summary>
    /// 根据重力 决定 up轴
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public static Vector3 GetUpAxis(Vector3 position)
    {
        Vector3 up = position.normalized;
        return Physics.gravity.y < 0f ? up : -up;
    }
}
