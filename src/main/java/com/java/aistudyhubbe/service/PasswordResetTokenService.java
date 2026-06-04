package com.java.aistudyhubbe.service;

import com.java.aistudyhubbe.entity.PasswordResetToken;
import com.java.aistudyhubbe.entity.User;
import com.java.aistudyhubbe.exception.InvalidTokenException;
import com.java.aistudyhubbe.repository.PasswordResetTokenRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.UUID;

@Service
@RequiredArgsConstructor
public class PasswordResetTokenService {

    private final PasswordResetTokenRepository tokenRepository;
    private static final int EXPIRATION_MINUTES = 15;

    @Transactional
    public PasswordResetToken createPasswordResetToken(User user) {
        String tokenValue = UUID.randomUUID().toString();
        PasswordResetToken myToken = new PasswordResetToken();
        myToken.setToken(tokenValue);
        myToken.setUser(user);
        myToken.setExpiresAt(LocalDateTime.now().plusMinutes(EXPIRATION_MINUTES));
        return tokenRepository.save(myToken);
    }

    public PasswordResetToken validatePasswordResetToken(String token) {
        PasswordResetToken passToken = tokenRepository.findByToken(token)
                .orElseThrow(() -> new InvalidTokenException("Invalid password reset token"));

        if (passToken.getExpiresAt().isBefore(LocalDateTime.now())) {
            tokenRepository.delete(passToken);
            throw new InvalidTokenException("Expired password reset token");
        }
        return passToken;
    }

    public void deleteToken(PasswordResetToken token) {
        tokenRepository.delete(token);
    }
}
