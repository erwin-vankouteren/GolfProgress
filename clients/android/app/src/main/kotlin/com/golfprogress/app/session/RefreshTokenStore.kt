package com.golfprogress.app.session

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import kotlinx.coroutines.flow.first
import java.security.GeneralSecurityException
import java.security.KeyStore
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/** Where the refresh token is kept between launches (#71). */
interface RefreshTokenStore {
    suspend fun load(): String?

    suspend fun save(token: String)

    suspend fun clear()
}

/** Encrypts the refresh token before it's written anywhere. */
interface TokenCipher {
    fun encrypt(plaintext: ByteArray): ByteArray

    fun decrypt(ciphertext: ByteArray): ByteArray
}

/**
 * The refresh token in DataStore, encrypted (#71). Without the cipher's key the stored
 * value is unreadable; the DataStore file is also excluded from backups and device
 * transfer (res/xml/data_extraction_rules.xml).
 *
 * If the value can't be decrypted (the key was lost, e.g. after a restore to another
 * device), it's dropped and the user signs in again.
 */
class EncryptedRefreshTokenStore(
    private val dataStore: DataStore<Preferences>,
    private val cipher: TokenCipher,
) : RefreshTokenStore {
    override suspend fun load(): String? {
        val stored = dataStore.data.first()[refreshTokenKey] ?: return null
        return try {
            cipher.decrypt(Base64.getDecoder().decode(stored)).toString(Charsets.UTF_8)
        } catch (_: GeneralSecurityException) {
            clear()
            null
        } catch (_: IllegalArgumentException) {
            clear()
            null
        }
    }

    override suspend fun save(token: String) {
        val encrypted = Base64.getEncoder().encodeToString(cipher.encrypt(token.toByteArray(Charsets.UTF_8)))
        dataStore.edit { it[refreshTokenKey] = encrypted }
    }

    override suspend fun clear() {
        dataStore.edit { it.remove(refreshTokenKey) }
    }

    private companion object {
        val refreshTokenKey = stringPreferencesKey("refresh_token")
    }
}

/**
 * AES-GCM with a key generated inside the Android Keystore: the key never leaves it, so
 * the stored token can only be decrypted by this app on this device.
 */
class KeystoreTokenCipher(
    private val alias: String = "golfprogress_refresh_token",
) : TokenCipher {
    override fun encrypt(plaintext: ByteArray): ByteArray {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, key())
        return cipher.iv + cipher.doFinal(plaintext)
    }

    override fun decrypt(ciphertext: ByteArray): ByteArray {
        if (ciphertext.size <= IV_BYTES) throw GeneralSecurityException("Ciphertext too short.")
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(TAG_BITS, ciphertext, 0, IV_BYTES))
        return cipher.doFinal(ciphertext, IV_BYTES, ciphertext.size - IV_BYTES)
    }

    @Synchronized
    private fun key(): SecretKey {
        val keyStore = KeyStore.getInstance(KEYSTORE).apply { load(null) }
        (keyStore.getKey(alias, null) as? SecretKey)?.let { return it }

        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE)
        generator.init(
            KeyGenParameterSpec
                .Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .build(),
        )
        return generator.generateKey()
    }

    private companion object {
        const val KEYSTORE = "AndroidKeyStore"
        const val TRANSFORMATION = "AES/GCM/NoPadding"
        const val IV_BYTES = 12
        const val TAG_BITS = 128
    }
}
