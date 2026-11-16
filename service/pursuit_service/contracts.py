from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, FiniteFloat, StringConstraints, model_validator

Identifier = Annotated[str, StringConstraints(min_length=1, max_length=96)]


class StrictModel(BaseModel):
    model_config = ConfigDict(extra="forbid", allow_inf_nan=False)


class Point(StrictModel):
    x: FiniteFloat
    y: FiniteFloat
    z: FiniteFloat


class KnownSuspect(StrictModel):
    position: Point
    heading: FiniteFloat
    speed: Annotated[FiniteFloat, Field(ge=0)]
    observed_ms: int = Field(ge=0)
    vehicle_description: str = Field(max_length=256)
    observer_id: Identifier


class UnitState(StrictModel):
    id: Identifier
    position: Point
    available: Literal[True]


class RoadCandidate(StrictModel):
    id: Identifier
    position: Point
    reachable_unit_ids: list[Identifier] = Field(max_length=128)
    description: str = Field(max_length=256)
    suspect_route_verified: bool


class Assignment(StrictModel):
    unit_id: Identifier
    role: Literal["lead", "support", "intercept", "contain"]
    destination_node_id: Identifier


class CandidatePlan(StrictModel):
    id: Identifier
    description: str = Field(min_length=1, max_length=2048)
    assignments: list[Assignment] = Field(min_length=1, max_length=128)


class Snapshot(StrictModel):
    schema_version: Literal[1]
    session_id: Identifier
    revision: int = Field(ge=0)
    captured_ms: int = Field(ge=0)
    wanted_level: int = Field(ge=1, le=5)
    known_suspect: KnownSuspect
    visual_contact: bool
    units: list[UnitState] = Field(min_length=1, max_length=128)
    roads: list[RoadCandidate] = Field(min_length=1, max_length=256)
    candidates: list[CandidatePlan] = Field(min_length=1, max_length=8)

    @model_validator(mode="after")
    def validate_references(self):
        units = {u.id for u in self.units}
        roads = {r.id: r for r in self.roads}
        if len(units) != len(self.units) or len(roads) != len(self.roads):
            raise ValueError("duplicate unit or road")
        if self.known_suspect.observed_ms > self.captured_ms:
            raise ValueError("future sighting")
        if len({p.id for p in self.candidates}) != len(self.candidates):
            raise ValueError("duplicate candidate")
        for road in self.roads:
            if not set(road.reachable_unit_ids) <= units:
                raise ValueError("unknown reachable unit")
        for plan in self.candidates:
            assigned = [a.unit_id for a in plan.assignments]
            if len(assigned) != len(units) or set(assigned) != units:
                raise ValueError("plan must assign every managed unit exactly once")
            if sum(a.role == "lead" for a in plan.assignments) > 1:
                raise ValueError("multiple lead units")
            for assignment in plan.assignments:
                road = roads.get(assignment.destination_node_id)
                if road is None or assignment.unit_id not in road.reachable_unit_ids:
                    raise ValueError("unknown or unreachable destination")
                if assignment.role == "intercept" and not road.suspect_route_verified:
                    raise ValueError("interception route not verified from suspect report")
        return self


class DecisionResponse(StrictModel):
    session_id: Identifier
    revision: int = Field(ge=0)
    candidate_id: Identifier
    probabilities: dict[Identifier, Annotated[FiniteFloat, Field(ge=0, le=1)]]
    inference_ms: Annotated[FiniteFloat, Field(ge=0)]
    status: Literal["selected"] = "selected"

    def validate_for(self, snapshot: Snapshot):
        candidates = {c.id for c in snapshot.candidates}
        if (self.session_id, self.revision) != (snapshot.session_id, snapshot.revision):
            raise ValueError("response identity mismatch")
        if self.candidate_id not in candidates or set(self.probabilities) != candidates:
            raise ValueError("unknown or missing choices")
        if abs(sum(self.probabilities.values()) - 1) > 0.02:
            raise ValueError("probabilities must sum to one")
        return self
