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


#endif