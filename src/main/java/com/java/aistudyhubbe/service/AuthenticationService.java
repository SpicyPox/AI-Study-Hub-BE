package com.java.aistudyhubbe.service;

import com.java.aistudyhubbe.config.security.JwtService;
import com.java.aistudyhubbe.dto.auth.AuthenticationRequest;
import com.java.aistudyhubbe.dto.auth.AuthenticationResponse;
import com.java.aistudyhubbe.dto.auth.RegisterRequest;
import com.java.aistudyhubbe.entity.PasswordResetToken;
import com.java.aistudyhubbe.entity.RefreshToken;
import com.java.aistudyhubbe.entity.Role;
import com.java.aistudyhubbe.entity.User;
import com.java.aistudyhubbe.exception.RefreshTokenNotFoundException;
import com.java.aistudyhubbe.exception.UserNotFoundException;
import com.java.aistudyhubbe.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.security.authentication.AuthenticationManager;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.UUID;

@Service
@RequiredArgsConstructor
public class AuthenticationService {

    private static final Logger log = LoggerFactory.getLogger(AuthenticationService.class);

    private final UserRepository userRepository;
    private final PasswordEncoder passwordEncoder;
    private final JwtService jwtService;
    private final AuthenticationManager authenticationManager;
    private final RefreshTokenService refreshTokenService;
    private final PasswordResetTokenService passwordResetTokenService;
    private final EmailService emailService;

    @Value("${application.client-url}")
    private String clientUrl;

    @Transactional
    public AuthenticationResponse register(RegisterRequest request) {
        User user = new User();
        user.setUsername(request.getUsername());
        user.setEmail(request.getEmail());
        String encodedPassword = passwordEncoder.encode(request.getPassword());
        user.setPassword(encodedPassword);
        user.setRole(request.getRole() != null ? request.getRole() : Role.USER);
        User savedUser = userRepository.save(user);
        String jwtToken = jwtService.generateToken(savedUser);
        RefreshToken refreshToken = refreshTokenService.createOrUpdateRefreshToken(savedUser.getId());
        return AuthenticationResponse.builder()
                .accessToken(jwtToken)
                .refreshToken(refreshToken.getToken())
                .build();
    }

    @Transactional
    public AuthenticationResponse authenticate(AuthenticationRequest request) {
        authenticationManager.authenticate(
                new UsernamePasswordAuthenticationToken(request.getUsername(), request.getPassword())
        );
        User user = userRepository.findByUsername(request.getUsername())
                .orElseThrow(() -> new UserNotFoundException("User not found"));
        String jwtToken = jwtService.generateToken(user);
        RefreshToken refreshToken = refreshTokenService.createOrUpdateRefreshToken(user.getId());
        return AuthenticationResponse.builder()
                .accessToken(jwtToken)
                .refreshToken(refreshToken.getToken())
                .build();
    }

    public AuthenticationResponse refreshToken(String refreshToken) {
        return refreshTokenService.findByToken(refreshToken)
                .map(refreshTokenService::verifyExpiration)
                .map(RefreshToken::getUser)
                .map(user -> {
                    String accessToken = jwtService.generateToken(user);
                    return AuthenticationResponse.builder()
                            .accessToken(accessToken)
                            .refreshToken(refreshToken)
                            .build();
                })
                .orElseThrow(() -> new RefreshTokenNotFoundException("Refresh token is not in database!"));
    }

    public void logout() {
        Object principal = SecurityContextHolder.getContext().getAuthentication().getPrincipal();
        if (principal instanceof User) {
            UUID userId = ((User) principal).getId();
            refreshTokenService.deleteByUserId(userId);
        } else {
            throw new UserNotFoundException("No authenticated user found to logout.");
        }
    }

    @Transactional
    public void forgotPassword(String email) {
        User user = userRepository.findByEmail(email)
                .orElseThrow(() -> new UserNotFoundException("User with email " + email + " not found"));
        PasswordResetToken token = passwordResetTokenService.createPasswordResetToken(user);
        String resetUrl = clientUrl + "/reset-password?token=" + token.getToken();
        String emailText = "To reset your password, click the link below:\n" + resetUrl;
        emailService.sendEmail(user.getEmail(), "Password Reset Request", emailText);
    }

    @Transactional
    public void resetPassword(String token, String newPassword) {
        PasswordResetToken passToken = passwordResetTokenService.validatePasswordResetToken(token);
        User user = passToken.getUser();
        user.setPassword(passwordEncoder.encode(newPassword));
        userRepository.save(user);
        passwordResetTokenService.deleteToken(passToken);
    }
}
