# Particle Emission Volumes Reference - From Generals Code

This document contains code snippets from the original C&C Generals reference implementation showing how particle emission volumes work.

## EmissionVolumeType Enum

**Source:** [Generals/Code/GameEngine/Include/GameClient/ParticleSys.h#L420](references/generals_code/Generals/Code/GameEngine/Include/GameClient/ParticleSys.h)

```cpp
enum EmissionVolumeType
{
    INVALID_VOLUME=0, POINT, LINE, BOX, SPHERE, CYLINDER
}
m_emissionVolumeType;  ///< the type of volume where particles are created
```

**Volume Type Names:**
```cpp
static char *EmissionVolumeTypeNames[] =
{
    "NONE", "POINT", "LINE", "BOX", "SPHERE", "CYLINDER", NULL
};
```

## Emission Volume Union Structure

**Source:** [Generals/Code/GameEngine/Include/GameClient/ParticleSys.h#L424-L450](references/generals_code/Generals/Code/GameEngine/Include/GameClient/ParticleSys.h)

```cpp
union emissionVolumeUnion
{
    // point just uses system's position

    // line
    struct
    {
        Coord3D start, end;
    }
    line;

    // box
    struct
    {
        Coord3D halfSize;
    }
    box;

    // sphere
    struct
    {
        Real radius;
    }
    sphere;

    // cylinder
    struct
    {
        Real radius;
        Real length;
    }
    cylinder;
}
m_emissionVolume;  ///< the dimensions of the emission volume

Bool m_isEmissionVolumeHollow;  ///< if true, only create particles at boundary of volume
```

## Particle Emission Position Logic

**Source:** [Generals/Code/GameEngine/Source/GameClient/System/ParticleSys.cpp#L1650-L1760](references/generals_code/Generals/Code/GameEngine/Source/GameClient/System/ParticleSys.cpp)

The `computeParticlePosition()` function calculates a random position within the emission volume:

### POINT Volume (Default)
```cpp
case POINT:
default:
    newPos.x = 0.0f;
    newPos.y = 0.0f;
    newPos.z = 0.0f;
    break;
```

### CYLINDER Volume
```cpp
case CYLINDER:
{
    Real angle = GameClientRandomValueReal( 0, 2.0f*PI );
    Real radius;
    
    if (m_isEmissionVolumeHollow)
        radius = m_emissionVolume.cylinder.radius;
    else
        radius = GameClientRandomValueReal( 0.0f, m_emissionVolume.cylinder.radius );

    newPos.x = radius * cos( angle );
    newPos.y = radius * sin( angle );

    Real halfLength = m_emissionVolume.cylinder.length/2.0f;
    newPos.z = GameClientRandomValueReal( -halfLength, halfLength );

    break;
}
```

### SPHERE Volume
```cpp
case SPHERE:
{
    Real radius;

    if (m_isEmissionVolumeHollow)
        radius = m_emissionVolume.sphere.radius;
    else
        radius = GameClientRandomValueReal( 0.0f, m_emissionVolume.sphere.radius );

    newPos = *computePointOnUnitSphere();

    newPos.x *= radius;
    newPos.y *= radius;
    newPos.z *= radius;

    break;
}
```

### BOX Volume
```cpp
case BOX:
{			
    if (m_isEmissionVolumeHollow) {
        // determine which side to generate on.
        // 0 is bottom, 3 is top, 
        // 1 is left , 4 is right
        // 2 is front, 5 is back

        int side = GameClientRandomValue(0, 6);
        if (side % 3 == 0) {
            // generate X, Y
            newPos.x = GameClientRandomValueReal( -m_emissionVolume.box.halfSize.x, m_emissionVolume.box.halfSize.x );
            newPos.y = GameClientRandomValueReal( -m_emissionVolume.box.halfSize.y, m_emissionVolume.box.halfSize.y );
            if (side == 0) {
                newPos.z = -m_emissionVolume.box.halfSize.z;
            } else {
                newPos.z = m_emissionVolume.box.halfSize.z;
            }
                                                
        } else if (side % 3 == 1) {
            // generate Y, Z
            newPos.y = GameClientRandomValueReal( -m_emissionVolume.box.halfSize.y, m_emissionVolume.box.halfSize.y );
            newPos.z = GameClientRandomValueReal( -m_emissionVolume.box.halfSize.z, m_emissionVolume.box.halfSize.z );
            if (side == 1) {
                newPos.x = -m_emissionVolume.box.halfSize.x;
            } else {
                newPos.x = m_emissionVolume.box.halfSize.y;
            }

        } else if (side % 3 == 2) {
            // generate X, Z
            newPos.x = GameClientRandomValueReal( -m_emissionVolume.box.halfSize.x, m_emissionVolume.box.halfSize.x );
            newPos.z = GameClientRandomValueReal( -m_emissionVolume.box.halfSize.z, m_emissionVolume.box.halfSize.z );
            if (side == 2) {
                newPos.y = -m_emissionVolume.box.halfSize.y;
            } else {
                newPos.y = m_emissionVolume.box.halfSize.y;
            }
        }
    } else {
        newPos.x = GameClientRandomValueReal( -m_emissionVolume.box.halfSize.x, m_emissionVolume.box.halfSize.x );
        newPos.y = GameClientRandomValueReal( -m_emissionVolume.box.halfSize.y, m_emissionVolume.box.halfSize.y );
        newPos.z = GameClientRandomValueReal( -m_emissionVolume.box.halfSize.z, m_emissionVolume.box.halfSize.z );
        break;
    }
}
```

### LINE Volume
```cpp
case LINE:
{
    Coord3D delta, start, end;

    start = m_emissionVolume.line.start;
    end = m_emissionVolume.line.end;

    delta.x = end.x - start.x;
    delta.y = end.y - start.y;
    delta.z = end.z - start.z;

    Real t = GameClientRandomValueReal( 0.0f, 1.0f );

    newPos.x = start.x + t * delta.x;
    newPos.y = start.y + t * delta.y;
    newPos.z = start.z + t * delta.z;
    break;
}
```

### Position Scaling
All positions are scaled by particle scale factor:
```cpp
newPos.x *= (0.5f+TheGlobalData->m_particleScale/2.0f);
newPos.y *= (0.5f+TheGlobalData->m_particleScale/2.0f);
newPos.z *= (0.5f+TheGlobalData->m_particleScale/2.0f);
return &newPos;
```

## Particle Velocity Computation

**Source:** [Generals/Code/GameEngine/Source/GameClient/System/ParticleSys.cpp#L1495-L1640](references/generals_code/Generals/Code/GameEngine/Source/GameClient/System/ParticleSys.cpp)

The `computeParticleVelocity()` function takes a position and computes velocity. It supports several modes, with **OUTWARD** being the key mode for terrain effects:

### OUTWARD Velocity (For TerrainFire and Lightning)
```cpp
case OUTWARD:
{
    Real speed = m_emissionVelocity.outward.speed.getValue();
    Real otherSpeed = m_emissionVelocity.outward.otherSpeed.getValue();
    Coord3D sysPos;

    sysPos.x = 0.0f;
    sysPos.y = 0.0f;
    sysPos.z = 0.0f;

    switch( m_emissionVolumeType )
    {
        case CYLINDER:
            Coord2D disk;
            disk.x = pos->x - sysPos.x;
            disk.y = pos->y - sysPos.y;
            disk.normalize();

            newVel.x = speed * disk.x;
            newVel.y = speed * disk.y;
            newVel.z = otherSpeed;
            break;

        case BOX:
        case SPHERE:
        {
            newVel.x = pos->x - sysPos.x;
            newVel.y = pos->y - sysPos.y;
            newVel.z = pos->z - sysPos.z;
            newVel.normalize();

            newVel.x *= speed;
            newVel.y *= speed;
            newVel.z *= speed;
            break;
        }

        case LINE:
        {
            Coord3D along;  // unit vector along line direction

            along.x = m_emissionVolume.line.end.x - m_emissionVolume.line.start.x;
            along.y = m_emissionVolume.line.end.y - m_emissionVolume.line.start.y;
            along.z = m_emissionVolume.line.end.z - m_emissionVolume.line.start.z;
            along.normalize();

            Coord3D perp;  // unit vector perpendicular to the along/up plane
            Coord3D up;    // unit vector in the up direction (Z)
            up.x = 0.0;
            up.y = 0.0;
            up.z = 1.0;
            perp.crossProduct( &up, &along, &perp );
            up.crossProduct( &along, &perp, &up );

            // "speed" is in 'horizontal' plane, and "otherSpeed" is 'vertical'
            newVel.x = speed * perp.x + otherSpeed * up.x;
            newVel.y = speed * perp.y + otherSpeed * up.y;
            newVel.z = speed * perp.z + otherSpeed * up.z;								
            break;
        }

        case POINT:
        {
            Coord3D vel = *computePointOnUnitSphere();

            newVel.x = speed * vel.x;
            newVel.y = speed * vel.y;
            newVel.z = speed * vel.z;
            break;
        }
    }
    break;
}
```

## Random Sphere Point Generation

**Source:** [Generals/Code/GameEngine/Source/GameClient/System/ParticleSys.cpp#L1475-L1490](references/generals_code/Generals/Code/GameEngine/Source/GameClient/System/ParticleSys.cpp)

Used for spherical and hemispherical emissions:

```cpp
const Coord3D *ParticleSystem::computePointOnUnitSphere( void )
{
    static Coord3D point;

    do
    {
        point.x = GameClientRandomValueReal( -1.0f, 1.0f );
        point.y = GameClientRandomValueReal( -1.0f, 1.0f );
        point.z = GameClientRandomValueReal( -1.0f, 1.0f );
    }
    while (point.x == 0.0f && point.y == 0.0f && point.z == 0.0f);

    point.normalize();

    return &point;
}
```

## EmissionVelocityType Options

**Source:** [Generals/Code/GameEngine/Include/GameClient/ParticleSys.h#L270-L272](references/generals_code/Generals/Code/GameEngine/Include/GameClient/ParticleSys.h)

```cpp
enum EmissionVelocityType
{
    INVALID_VELOCITY=0, ORTHO, SPHERICAL, HEMISPHERICAL, CYLINDRICAL, OUTWARD
}

static char *EmissionVelocityTypeNames[] =
{
    "NONE", "ORTHO", "SPHERICAL", "HEMISPHERICAL", "CYLINDRICAL", "OUTWARD", NULL
};
```

## Key Insights for TerrainFire and Lightning

Based on the code analysis:

1. **No dedicated TerrainFire or Lightning volume types** - The original Generals engine only supports: POINT, LINE, BOX, SPHERE, CYLINDER

2. **TerrainFire likely uses:**
   - `SPHERE` volume type (emits in all directions from a point)
   - `OUTWARD` velocity type (particles expand outward from emission center)
   - Possibly `m_isEmissionVolumeHollow = true` to emit only at sphere surface

3. **Lightning likely uses:**
   - `LINE` or `CYLINDER` volume type (for the bolt path)
   - `OUTWARD` velocity type with minimal spread
   - Possibly `CYLINDER` with small radius and specific `length` for the beam

4. **Standard Emission Pattern:**
   - `GetRay()` equivalent is `computeParticlePosition()` - generates random position within volume
   - Velocity is determined by `computeParticleVelocity()` with volume-specific logic
   - All emissions respect `m_isEmissionVolumeHollow` flag for hollow volumes
   - Positions and velocities are scaled by `m_particleScale`

## Implementation Notes

- **Random values** use `GameClientRandomValueReal(min, max)` and `GameClientRandomValue(min, max)`
- **Positions** are computed in local/object space relative to the system center
- **Velocities** are adjusted by velocity coefficients: `m_velCoeff * (0.5f + m_particleScale/2.0f)`
- **Hollow volumes** only emit particles at volume boundaries
- **The union approach** allows compact storage of different volume parameters
