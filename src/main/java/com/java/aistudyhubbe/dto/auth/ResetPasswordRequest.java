package com.java.aistudyhubbe.dto.auth;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;
import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class ResetPasswordRequest {
    @NotBlank(message = "Token cannot be empty")
    private String token;

    @NotBlank(message = "New password cannot be empty")
    @Size(min = 6, message = "Password must be at least 6 characters")
    private String newPassword;
}
