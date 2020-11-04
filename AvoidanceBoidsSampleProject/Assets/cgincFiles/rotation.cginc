#ifndef ROTATION_INCLUDED
#define ROTATION_INCLUDED

#include "UnityCG.cginc"
#define Deg2Rad 0.0174532924

float4x4 QuaternionToMatrix(float4 quaternion) {

    float x = quaternion.x, y = quaternion.y, z = quaternion.z, w = quaternion.w;
    float xy = x*y*2, yz = y*z*2, xz = x*z*2, wx = w*x*2, wy = w*y*2, wz = w*z*2;
    float xx = x*x*2, yy = y*y*2, zz = z*z*2;
    return float4x4(
        float4(1-yy-zz, xy+wz, xz-wy, 0),
        float4(xy-wz, 1-xx-zz, yz+wx, 0),
        float4(xz+wy, yz-wx, 1-xx-yy, 0),
        float4(0, 0, 0, 1)
    );

}

float4x4 EulerAnglesToMatrix(float3 angles) {

    float cx = cos(angles.x * Deg2Rad); float sx = sin(angles.x * Deg2Rad);
    float cy = cos(angles.z * Deg2Rad); float sy = sin(angles.z * Deg2Rad);
    float cz = cos(angles.y * Deg2Rad); float sz = sin(angles.y * Deg2Rad);

    return float4x4(
        cz*cy + sz*sx*sy, -cz*sy + sz*sx*cy, sz*cx, 0,
        sy*cx, cy*cx, -sx, 0,
        -sz*cy + cz*sx*sy, sy*sz + cz*sx*cy, cz*cx, 0,
        0, 0, 0, 1
    );
}

float4x4 quaternionToMat(float4 q){
    float4x4 m;
    m._11 = 1.0f - 2.0f * q.y * q.y - 2.0f * q.z * q.z;
    m._12 = 2.0f * q.x * q.y + 2.0f * q.w * q.z;
    m._13 = 2.0f * q.x * q.z - 2.0f * q.w * q.y;
    m._14 = 0;

    m._21 = 2.0f * q.x * q.y - 2.0f * q.w * q.z;
    m._22 = 1.0f - 2.0f * q.x * q.x - 2.0f * q.z * q.z;
    m._23 = 2.0f * q.y * q.z + 2.0f * q.w * q.x;
    m._24 = 0;

    m._31 = 2.0f * q.x * q.z + 2.0f * q.w * q.y;
    m._32 = 2.0f * q.y * q.z - 2.0f * q.w * q.x;
    m._33 = 1.0f - 2.0f * q.x * q.x - 2.0f * q.y * q.y;
    m._34 = 0;

    m._41_42_43_44 = float4(0,0,0,1);
    return m;
}

#endif