import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  Alert,
} from 'react-native';
import * as WebBrowser from 'expo-web-browser';
import { makeRedirectUri } from 'expo-auth-session';
import { supabase } from '../services/supabase';

WebBrowser.maybeCompleteAuthSession();

type Mode = 'login' | 'signup';

export default function AuthScreen() {
  const [mode, setMode] = useState<Mode>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [oauthLoading, setOauthLoading] = useState<'google' | 'github' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const redirectUri = makeRedirectUri({ scheme: 'notetaker' });

  const clearMessages = () => {
    setError(null);
    setSuccessMsg(null);
  };

  // ── Email / Password ───────────────────────────────────────

  const handleEmailAuth = async () => {
    clearMessages();
    if (!email.trim() || !password.trim()) {
      setError('Please enter your email and password.');
      return;
    }
    setLoading(true);
    try {
      if (mode === 'login') {
        const { error } = await supabase.auth.signInWithPassword({ email: email.trim(), password });
        if (error) setError(error.message);
      } else {
        const { error } = await supabase.auth.signUp({ email: email.trim(), password });
        if (error) {
          setError(error.message);
        } else {
          setSuccessMsg('Account created! Check your email to confirm, then log in.');
          setMode('login');
        }
      }
    } finally {
      setLoading(false);
    }
  };

  // ── OAuth ──────────────────────────────────────────────────

  const handleOAuth = async (provider: 'google' | 'github') => {
    clearMessages();
    setOauthLoading(provider);
    try {
      const { data, error } = await supabase.auth.signInWithOAuth({
        provider,
        options: {
          redirectTo: redirectUri,
          skipBrowserRedirect: true,
        },
      });

      if (error) {
        setError(error.message);
        return;
      }

      if (!data.url) {
        setError('Could not get authorization URL.');
        return;
      }

      const result = await WebBrowser.openAuthSessionAsync(data.url, redirectUri);

      if (result.type === 'success' && result.url) {
        // Extract tokens from the callback URL and set session
        const url = new URL(result.url);
        const accessToken = url.searchParams.get('access_token') ??
          result.url.match(/access_token=([^&]+)/)?.[1];
        const refreshToken = url.searchParams.get('refresh_token') ??
          result.url.match(/refresh_token=([^&]+)/)?.[1];

        if (accessToken && refreshToken) {
          await supabase.auth.setSession({
            access_token: accessToken,
            refresh_token: refreshToken,
          });
        } else {
          // Fallback: let Supabase detect session from URL
          await supabase.auth.getSession();
        }
      }
    } catch (e: any) {
      setError(e.message ?? 'OAuth sign-in failed.');
    } finally {
      setOauthLoading(null);
    }
  };

  // ── Render ─────────────────────────────────────────────────

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView contentContainerStyle={styles.inner} keyboardShouldPersistTaps="handled">

        {/* Brand */}
        <View style={styles.brand}>
          <Text style={styles.brandIcon}>📋</Text>
          <Text style={styles.brandName}>NoteKeeper</Text>
          <Text style={styles.brandSub}>Your personal note hub</Text>
        </View>

        {/* Mode toggle */}
        <View style={styles.modeRow}>
          <TouchableOpacity
            style={[styles.modeBtn, mode === 'login' && styles.modeBtnActive]}
            onPress={() => { setMode('login'); clearMessages(); }}
          >
            <Text style={[styles.modeBtnText, mode === 'login' && styles.modeBtnTextActive]}>
              Log In
            </Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.modeBtn, mode === 'signup' && styles.modeBtnActive]}
            onPress={() => { setMode('signup'); clearMessages(); }}
          >
            <Text style={[styles.modeBtnText, mode === 'signup' && styles.modeBtnTextActive]}>
              Sign Up
            </Text>
          </TouchableOpacity>
        </View>

        {/* Email + Password */}
        <TextInput
          style={styles.input}
          placeholder="Email"
          placeholderTextColor="#94a3b8"
          autoCapitalize="none"
          keyboardType="email-address"
          value={email}
          onChangeText={setEmail}
        />
        <TextInput
          style={styles.input}
          placeholder="Password"
          placeholderTextColor="#94a3b8"
          secureTextEntry
          value={password}
          onChangeText={setPassword}
        />

        {/* Error / success messages */}
        {error && <Text style={styles.errorText}>{error}</Text>}
        {successMsg && <Text style={styles.successText}>{successMsg}</Text>}

        {/* Primary button */}
        <TouchableOpacity
          style={styles.primaryBtn}
          onPress={handleEmailAuth}
          disabled={loading}
        >
          {loading
            ? <ActivityIndicator color="#fff" />
            : <Text style={styles.primaryBtnText}>
                {mode === 'login' ? 'Log In' : 'Create Account'}
              </Text>
          }
        </TouchableOpacity>

        {/* Divider */}
        <View style={styles.dividerRow}>
          <View style={styles.dividerLine} />
          <Text style={styles.dividerText}>or continue with</Text>
          <View style={styles.dividerLine} />
        </View>

        {/* OAuth buttons */}
        <TouchableOpacity
          style={styles.oauthBtn}
          onPress={() => handleOAuth('google')}
          disabled={oauthLoading !== null}
        >
          {oauthLoading === 'google'
            ? <ActivityIndicator color="#1e293b" />
            : <Text style={styles.oauthBtnText}>🔵  Continue with Google</Text>
          }
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.oauthBtn}
          onPress={() => handleOAuth('github')}
          disabled={oauthLoading !== null}
        >
          {oauthLoading === 'github'
            ? <ActivityIndicator color="#1e293b" />
            : <Text style={styles.oauthBtnText}>⚫  Continue with GitHub</Text>
          }
        </TouchableOpacity>

      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f8fafc',
  },
  inner: {
    flexGrow: 1,
    justifyContent: 'center',
    padding: 28,
  },
  brand: {
    alignItems: 'center',
    marginBottom: 36,
  },
  brandIcon: {
    fontSize: 48,
    marginBottom: 8,
  },
  brandName: {
    fontSize: 26,
    fontWeight: '700',
    color: '#1e293b',
  },
  brandSub: {
    fontSize: 14,
    color: '#94a3b8',
    marginTop: 4,
  },
  modeRow: {
    flexDirection: 'row',
    backgroundColor: '#e2e8f0',
    borderRadius: 10,
    padding: 4,
    marginBottom: 20,
  },
  modeBtn: {
    flex: 1,
    paddingVertical: 8,
    alignItems: 'center',
    borderRadius: 8,
  },
  modeBtnActive: {
    backgroundColor: '#fff',
    shadowColor: '#000',
    shadowOpacity: 0.08,
    shadowRadius: 4,
    elevation: 2,
  },
  modeBtnText: {
    fontSize: 14,
    color: '#64748b',
    fontWeight: '500',
  },
  modeBtnTextActive: {
    color: '#6366f1',
    fontWeight: '700',
  },
  input: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#e2e8f0',
    borderRadius: 10,
    padding: 14,
    fontSize: 15,
    color: '#1e293b',
    marginBottom: 12,
  },
  errorText: {
    color: '#ef4444',
    fontSize: 13,
    marginBottom: 10,
    textAlign: 'center',
  },
  successText: {
    color: '#10b981',
    fontSize: 13,
    marginBottom: 10,
    textAlign: 'center',
  },
  primaryBtn: {
    backgroundColor: '#6366f1',
    borderRadius: 10,
    padding: 15,
    alignItems: 'center',
    marginTop: 4,
  },
  primaryBtnText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '700',
  },
  dividerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginVertical: 20,
    gap: 10,
  },
  dividerLine: {
    flex: 1,
    height: 1,
    backgroundColor: '#e2e8f0',
  },
  dividerText: {
    fontSize: 13,
    color: '#94a3b8',
  },
  oauthBtn: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#e2e8f0',
    borderRadius: 10,
    padding: 14,
    alignItems: 'center',
    marginBottom: 10,
  },
  oauthBtnText: {
    fontSize: 15,
    color: '#1e293b',
    fontWeight: '500',
  },
});
