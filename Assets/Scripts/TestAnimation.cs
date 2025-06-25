using UnityEngine;
using System.Collections;

public class TestAnimation : MonoBehaviour
{
    Animator Test;

    void Start()
    {
        Test = GetComponent<Animator>();
    }

    void Update()
    {
        for (int i = 0; i <= 6; i++)
        {
            if (Input.GetKeyDown(i.ToString()))
            {
                HandleKeyPress(i);
            }
        }
    }

    void HandleKeyPress(int keyNumber)
    {

        if (keyNumber == 1)
        {
            Test.SetTrigger("Left_Head_Punch");
        }
        else if (keyNumber == 2)
        {
            Test.SetTrigger("Left_Body_Punch");

        }
        else if (keyNumber == 3) {
            Test.SetTrigger("Right_Head_Punch");
        }
        else if (keyNumber == 4)
        {
            Test.SetTrigger("Right_Body_Punch");
        }
        else if (keyNumber == 5)
        {
            Test.SetTrigger("Body_Block");
        }
        else if (keyNumber == 6)
        {
            Test.SetTrigger("Head_Block");
        }


    }
}