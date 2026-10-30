using UnityEngine;

// 挂在玩家根上：第几名玩家、性别（只是数据），以及常用组件的引用。Animator 允许为空（无骨骼退路）。
public class PlayerContext : MonoBehaviour
{
    public int playerIndex;
    public Gender gender;
    public PlayerController controller;
    public FishingStateMachine fishing;
    public Animator animator;
}
