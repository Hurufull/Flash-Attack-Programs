using UnityEngine;

public class CloudMovement : MonoBehaviour
{
    // インスペクターで雲の移動速度を設定できる
    public float moveSpeed = 0.5f;

    // ループさせるための設定
    public float resetPositionX = -20f; // 画面の左端（ここまで来たらワープさせる）
    public float startPositionX = 20f;  // 画面の右端（ワープした後のスタート位置）

    void Update()
    {
        // オブジェクトを左にじわじわ動かす
        transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);

        // 指定した左端の座標まで移動したら、右端へワープさせる
        if (transform.position.x <= resetPositionX)
        {
            // Y座標とZ座標はそのままに、X座標だけスタート位置に戻す
            transform.position = new Vector3(startPositionX, transform.position.y, transform.position.z);
        }
    }
}
