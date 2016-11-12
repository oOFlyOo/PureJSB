﻿
//------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by CSGenerator.
// </auto-generated>
//------------------------------------------------------------------------------
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using jsval = JSApi.jsval;

public class JSB_UnityEngine_DynamicGI
{

////////////////////// DynamicGI ///////////////////////////////////////
// constructors

static bool DynamicGI_DynamicGI1(JSVCall vc, int argc)
{
    int _this = JSApi.getObject((int)JSApi.GetType.Arg);
    JSApi.attachFinalizerObject(_this);
    --argc;

    int len = argc;
    if (len == 0)
    {
        JSMgr.addJSCSRel(_this, new UnityEngine.DynamicGI());
    }

    return true;
}

// fields

// properties
static void DynamicGI_indirectScale(JSVCall vc)
{
    if (vc.bGet)
    { 
        var result = UnityEngine.DynamicGI.indirectScale;
                JSApi.setSingle((int)JSApi.SetType.Rval, (System.Single)(result));
    }
    else
    { 
        System.Single arg0 = (System.Single)JSApi.getSingle((int)JSApi.GetType.Arg);
        UnityEngine.DynamicGI.indirectScale = arg0;
    }
}
static void DynamicGI_updateThreshold(JSVCall vc)
{
    if (vc.bGet)
    { 
        var result = UnityEngine.DynamicGI.updateThreshold;
                JSApi.setSingle((int)JSApi.SetType.Rval, (System.Single)(result));
    }
    else
    { 
        System.Single arg0 = (System.Single)JSApi.getSingle((int)JSApi.GetType.Arg);
        UnityEngine.DynamicGI.updateThreshold = arg0;
    }
}
static void DynamicGI_synchronousMode(JSVCall vc)
{
    if (vc.bGet)
    { 
        var result = UnityEngine.DynamicGI.synchronousMode;
                JSApi.setBooleanS((int)JSApi.SetType.Rval, (System.Boolean)(result));
    }
    else
    { 
        System.Boolean arg0 = (System.Boolean)JSApi.getBooleanS((int)JSApi.GetType.Arg);
        UnityEngine.DynamicGI.synchronousMode = arg0;
    }
}

// methods

static bool DynamicGI_SetEmissive__Renderer__Color(JSVCall vc, int argc)
{
    int len = argc;
    if (len == 2) 
    {
        UnityEngine.Renderer arg0 = (UnityEngine.Renderer)JSMgr.datax.getObject((int)JSApi.GetType.Arg);
        UnityEngine.Color arg1 = (UnityEngine.Color)JSMgr.datax.getObject((int)JSApi.GetType.Arg);
        UnityEngine.DynamicGI.SetEmissive(arg0, arg1);
    }

    return true;
}

static bool DynamicGI_UpdateEnvironment(JSVCall vc, int argc)
{
    int len = argc;
    if (len == 0) 
    {
        UnityEngine.DynamicGI.UpdateEnvironment();
    }

    return true;
}

static bool DynamicGI_UpdateMaterials__Terrain__Int32__Int32__Int32__Int32(JSVCall vc, int argc)
{
    int len = argc;
    if (len == 5) 
    {
        UnityEngine.Terrain arg0 = (UnityEngine.Terrain)JSMgr.datax.getObject((int)JSApi.GetType.Arg);
        System.Int32 arg1 = (System.Int32)JSApi.getInt32((int)JSApi.GetType.Arg);
        System.Int32 arg2 = (System.Int32)JSApi.getInt32((int)JSApi.GetType.Arg);
        System.Int32 arg3 = (System.Int32)JSApi.getInt32((int)JSApi.GetType.Arg);
        System.Int32 arg4 = (System.Int32)JSApi.getInt32((int)JSApi.GetType.Arg);
        UnityEngine.DynamicGI.UpdateMaterials(arg0, arg1, arg2, arg3, arg4);
    }

    return true;
}

static bool DynamicGI_UpdateMaterials__Renderer(JSVCall vc, int argc)
{
    int len = argc;
    if (len == 1) 
    {
        UnityEngine.Renderer arg0 = (UnityEngine.Renderer)JSMgr.datax.getObject((int)JSApi.GetType.Arg);
        UnityEngine.DynamicGI.UpdateMaterials(arg0);
    }

    return true;
}

static bool DynamicGI_UpdateMaterials__Terrain(JSVCall vc, int argc)
{
    int len = argc;
    if (len == 1) 
    {
        UnityEngine.Terrain arg0 = (UnityEngine.Terrain)JSMgr.datax.getObject((int)JSApi.GetType.Arg);
        UnityEngine.DynamicGI.UpdateMaterials(arg0);
    }

    return true;
}


//register

public static void __Register()
{
    JSMgr.CallbackInfo ci = new JSMgr.CallbackInfo();
    ci.type = typeof(UnityEngine.DynamicGI);
    ci.fields = new JSMgr.CSCallbackField[]
    {

    };
    ci.properties = new JSMgr.CSCallbackProperty[]
    {
        DynamicGI_indirectScale,
        DynamicGI_updateThreshold,
        DynamicGI_synchronousMode,

    };
    ci.constructors = new JSMgr.MethodCallBackInfo[]
    {
        new JSMgr.MethodCallBackInfo(DynamicGI_DynamicGI1, ".ctor"),

    };
    ci.methods = new JSMgr.MethodCallBackInfo[]
    {
        new JSMgr.MethodCallBackInfo(DynamicGI_SetEmissive__Renderer__Color, "SetEmissive"),
        new JSMgr.MethodCallBackInfo(DynamicGI_UpdateEnvironment, "UpdateEnvironment"),
        new JSMgr.MethodCallBackInfo(DynamicGI_UpdateMaterials__Terrain__Int32__Int32__Int32__Int32, "UpdateMaterials"),
        new JSMgr.MethodCallBackInfo(DynamicGI_UpdateMaterials__Renderer, "UpdateMaterials"),
        new JSMgr.MethodCallBackInfo(DynamicGI_UpdateMaterials__Terrain, "UpdateMaterials"),

    };
    JSMgr.allCallbackInfo.Add(ci);
}


}
