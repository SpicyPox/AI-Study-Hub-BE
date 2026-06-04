package com.java.aistudyhubbe.controller;

import com.java.aistudyhubbe.dto.auth.AuthenticationRequest;
import com.java.aistudyhubbe.dto.auth.AuthenticationResponse;
import com.java.aistudyhubbe.dto.auth.ForgotPasswordRequest;
import com.java.aistudyhubbe.dto.auth.RefreshTokenRequest;
import com.java.aistudyhubbe.dto.auth.RegisterRequest;
import com.java.aistudyhubbe.dto.auth.ResetPasswordRequest;
import com.java.aistudyhubbe.service.AuthenticationService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/v1/auth")
@RequiredArgsConstructor
public class AuthenticationController {

    private final AuthenticationService service;

    @PostMapping("/register")
    public ResponseEntity<AuthenticationResponse> register(@Valid @RequestBody RegisterRequest request) {
        return ResponseEntity.ok(service.register(request));
    }

    @PostMapping("/authenticate")
    public ResponseEntity<AuthenticationResponse> authenticate(@Valid @RequestBody AuthenticationRequest request) {
        return ResponseEntity.ok(service.authenticate(request));
    }

    @PostMapping("/refresh-token")
    public ResponseEntity<AuthenticationResponse> refreshToken(@Valid @RequestBody RefreshTokenRequest request) {
        return ResponseEntity.ok(service.refreshToken(request.getRefreshToken()));
    }

    @PostMapping("/logout")
    public ResponseEntity<String> logout() {
        service.logout();
        return ResponseEntity.ok("User logged out successfully.");
    }

    @PostMapping("/forgot-password")
    public ResponseEntity<String> forgotPassword(@Valid @RequestBody ForgotPasswordRequest request) {
        service.forgotPassword(request.getEmail());
        return ResponseEntity.ok("Password reset link sent to your email.");
    }

    @PostMapping("/reset-password")
    public ResponseEntity<String> resetPassword(@Valid @RequestBody ResetPasswordRequest request) {
        service.resetPassword(request.getToken(), request.getNewPassword());
        return ResponseEntity.ok("Password has been reset successfully.");
    }
}
