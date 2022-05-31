using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CustomGravity 
{
    public static Vector3 gravityOrigin = Vector3.zero;//重心原点, 小行星的中心, 也就是小行星的位置

    /// <summary>
    /// 获取自定义重力向量的静态方法
    /// </summary>
    /// <returns></returns>
    public static Vector3 GetGravity(Vector3 position)
    {
        return (position - gravityOrigin).normalized * Physics.gravity.y ;
    }

    /// <summary>
    /// 获取自定义重力向量的静态方法, 有一个OUT 参数 用来返回根据重力 决定的 up轴, GetGravity的重载方法overload
    /// </summary>
    /// <returns></returns>
    public static Vector3 GetGravity(Vector3 position , out Vector3 upAxis) 
    {
        Vector3 up = (position - gravityOrigin).normalized;
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
        Vector3 up = (position - gravityOrigin).normalized;
        return Physics.gravity.y < 0f ? up : -up;
    }
}
