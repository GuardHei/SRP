#ifndef COMPUTE_UTILS
#define COMPUTE_UTILS

struct Cone {
    float3 vertex;
    float angle;
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
    float shadowStrength;
    Cone cone;
    float4x4 matrixVP;
    float smallAngle;
    float nearClip;
    uint shadowIndex;
};

CBUFFER_START(UnityPerFrame)
    float4 _ZBufferParams;
CBUFFER_END

inline float SinOf(float cos) {
    return sqrt(1 - cos * cos);
}

inline float TanOf(float sin, float cos) {
    return sin / cos;
}

inline float CosBetween(float3 directionA, float3 directionB) {
    return saturate(dot(directionA, directionB));
}

inline float SinBetween(float3 directionA, float3 directionB) {
    float cos = CosBetween(directionA, directionB);
    return sqrt(1 - cos * cos);
}

inline float TanBetween(float3 directionA, float3 directionB) {
    float cos = CosBetween(directionA, directionB);
    float sin = SinOf(cos);
    return TanOf(sin, cos);
}

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
    float3 direction = -cone.direction;
    float3 m = cross(cross(plane.xyz, direction), direction);
    float3 q = cone.vertex + direction * cone.height + normalize(m) * cone.radius;
    return VertexInsidePlane(cone.vertex, plane) + VertexInsidePlane(q, plane);
}

inline float VertexInsideSphere(float3 vertex, float4 sphere) {
    float3 distance = vertex - sphere.xyz;
    return dot(distance, distance) < sphere.w;
}

inline float SphereIntersect(float4 sphere, float4 plane) {
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

inline float ConeSphereIntersect(Cone cone, float4 sphere) {
    float3 diff = cone.vertex - sphere.xyz;
    float diffDot = dot(diff, diff);
    float lenComp = dot(diff, cone.direction);
    float dist = cos(cone.angle) * sqrt(diffDot - lenComp * lenComp) - lenComp * sin(cone.angle);

    bool angleCull = dist > sphere.w;
    bool frontCull = lenComp > sphere.w + cone.height;
    bool backCull = lenComp < -sphere.w;
    
    return !(angleCull || frontCull || backCull);
}

#endif // COMPUTE_UTILS