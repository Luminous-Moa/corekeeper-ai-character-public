# M 이동 manifest

기준 f512924. 각 형식/멤버 본문은 동일 토큰으로 이동했다. 원본/대상 경로는 Assets/AIPlayerSpike/Scripts 아래다. AIBodySkills→AIBody, AIExplore→AIMemory는 기존 GUID를 유지했다.

정적 초기화 순서는 멀티셋 검사와 별도로 확인했다. 이동 대상 38개 형식의 field/property initializer 선언 토큰 시퀀스가 원본과 같은 순서다(`m-field-order.txt`). AIBody.TestCommands 및 AIPlayerSpikeMod의 _pendingReplies/_renderChatText/_mentionNames/_repeatGate/ChatPatched 등 모든 필드는 원래 본체에 남았다. 멤버 token multiset 차이 0(`m-token-check.txt`), movecheck 잔여는 using/주석/허용 partial 선언/새 meta뿐이다(`m-movecheck.txt`).

| 원본 | 형식/멤버 | 대상 |
|---|---|---|
| AIBodySkills.cs | AIBody | AIBody.cs |
| AIBodySkills.cs | BodyInput,SkillStatus,AISkill | AISkill.cs |
| AIBodySkills.cs | AIVision | AIVision.cs |
| AIBodySkills.cs | AITileReader,AIWallData | AITileReader.cs |
| AIBodySkills.cs | SeenEnemy,AIObservation | AIObservation.cs |
| AIBodySkills.cs | NavStatus,AINavigator | AINavigator.cs |
| AIBodySkills.cs | LookSkill,InvSkill | AIInspectSkills.cs |
| AIBodySkills.cs | GotoSkill | AIGotoSkill.cs |
| AIBodySkills.cs | MineSkill | AIMineSkill.cs |
| AIBodySkills.cs | AttackSkill | AIAttackSkill.cs |
| AIBodySkills.cs | WalkTestSkill,AIMoveTrace | AIMoveTrace.cs |
| AICraft.cs | AICraft,CraftSkill | AICraft.cs |
| AICraft.cs | AIPlaceSpot,PlaceSkill | AIPlace.cs |
| AICraft.cs | RootSkill,BreakSkill | AIBreakSkills.cs |
| AIExplore.cs | AIMemory | AIMemory.cs |
| AIExplore.cs | AIDrops | AIDrops.cs |
| AIExplore.cs | AIObjects | AIObjects.cs |
| AIExplore.cs | TunnelPlan,AITunnel,TunnelSkill | AITunnel.cs |
| AIPlayerSpikeMod.cs | AIPlayerSpikeMod | AIPlayerSpikeMod.cs |
| AIPlayerSpikeMod.cs | ThinClientDriverConstructor,AIPlayerSpikePatches | AIPlayerSpikePatches.cs |
| AIPlayerSpikeMod.cs | AIWorldSettings | AIWorldSettings.cs |
| AIPlayerSpikeMod.cs | AICharacterStore | AICharacterStore.cs |
| AIEdge.cs | AIEdge | AIEdge.cs |
| AIEdge.cs | EdgeFillSkill,DoorPassSkill,DoorCloseSkill | AIEdgeSkills.cs |
| AIEdge.Guard.cs | AIEdge | AIEdge.Guard.cs |
| AIEdge.Guard.cs | PickupSkill | AIPickupSkill.cs |
| AIBody.cs | HandleCommand,TestOnlyWhere,TestOnlyTiles,IsSkillCommand,IsTestCommand,TestSwitchPath,TestCommandsOn | AIBody.Commands.cs |
| AIPlayerSpikeMod.cs | TryHandleChatCommand,MentionNames,TryHandleMention,IsAll,Without,DefaultAgent,Dispatch,IsAiCommand,StageText,StatusText,StatusAll,Reply,FlushReplies | AIPlayerSpikeMod.Chat.cs |
| AIEdge.Guard.cs | ReachRange,DoorsNear,OwnDoorTilesNow,GuardGridAround,ReachNow,MemoryNow,ShutDoorsNear,StandOk,EnemyTile,StandTile,MoveOf,ReachOf,_tileBreak,TileBreakOf,Threat,AnyThreat,CannotCome | AIEdge.Reach.cs |
