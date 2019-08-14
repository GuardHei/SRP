#ifndef COMPUTE_UTILS
#define COMPUTE_UTILS

struct Cone {
    float3 vertex;
    float height;
    float3 direction;
    float radius;
};

struct PointLight {
    float3 color;
    float4 sphere;
};

struct SpotLight {
    float3 color;
    Cone cone;
    float angle;
    float4x4 matrixVP;
    float smallAngle;
    float nearClip;
};

CBUFFER_START(UnityPerFrame)
    float4 _ZBufferParams;
CBUFFER_END

inline float4 GetPlane(float3 normal, float3 vertex) {
    return float4(normal, -dot(normal, vertex));
}

inline float4 GetPlane(float3 vertexA, float3 vertexB, float3 vertexC) {
    float3 normal = normalize(cross(vertexB - vertexA, vertexC - vertexA));
    return float4(normal, -dot(normal, vertexA));
}

inline float4 GetPlane(float4 vertexA, float4 vertexB, float4 vertexC) {
    float3 a = vertexA.xyz / vertexA.w;
    float3 b = vertexB.xyz / vertexB.w;
    float3 c = vertexC.xyz / vertexC.w;

    float3 normal = normalize(cross(b - a, c - a));
    return float4(normal, -dot(normal, a));
}

inline float GetDistanceToPlane(float4 plane, float3 vertex) {
    return dot(plane.xyz, vertex) + plane.w;
}

inline float VertexInsidePlane(float3 vertex, float4 plane) {
    return (dot(plane.xyz, vertex) + plane.w) < 0;
}

inline float SphereInsidePlane(float4 sphere, float4 plane) {
    return (dot(plane.xyz, sphere.xyz) + plane.w) < sphere.w;
}

inline float ConeInsidePlane(Cone cone, float4 plane) {
    float3 m = cross(cross(plane.xyz, cone.direction), cone.direction);
    float3 q = cone.vertex + cone.direction * cone.height + normalize(m) * cone.radius;
    return VertexInsidePlane(cone.vertex, plane) + VertexInsidePlane(q, plane);
}

float SphereIntersect(float4 sphere, float4 plane) {
    return (GetDistanceToPlane(plane, sphere.xyz) < sphere.w);
}

float SphereIntersect(float4 sphere, float4 planes[6]) {
    [unroll]
    for (uint i = 0; i < 6; ++i) {
        if (GetDistanceToPlane(planes[i], sphere.xyz) > sphere.w) return 0;
    }

    return 1;
}

float BoxIntersect(float3 extent, float3 position, float4 planes[6]) {
    float result = 1;
    for (uint i = 0; i < 6; ++i) {
        float4 plane = planes[i];
        float3 absNormal = abs(plane.xyz);
        result *= ((dot(position, plane.xyz) - dot(absNormal, extent)) < -plane.w);
    }

    return result;
}

float BoxIntersect(float3 extent, float3x3 localToWorld, float3 position, float4 planes[6]) {
    float result = 1;
    for (uint i = 0; i < 6; ++i) {
        float4 plane = planes[i];
        float3 absNormal = abs(mul(plane.xyz, localToWorld));
        result *= ((dot(position, plane.xyz) - dot(absNormal, extent)) < -plane.w);
    }

    return result;
}

float ConeIntersect(Cone cone, float4 planes[6]) {
    [unroll]
    for (uint i = 0; i < 6; ++i) {
        if (ConeInsidePlane(cone, planes[i]) < .5) return 0;
    }

    return 1;
}

inline float ConeIntersect(Cone cone, float4 plane) {
    return ConeInsidePlane(cone, plane);
}

#endif // COMPUTE_UTILS